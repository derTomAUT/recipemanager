using System.Text.Json;
using HtmlAgilityPack;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Services;

public class RecipeImportService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiRecipeImportService _aiImportService;

    public RecipeImportService(IHttpClientFactory httpClientFactory, AiRecipeImportService aiImportService)
    {
        _httpClientFactory = httpClientFactory;
        _aiImportService = aiImportService;
    }

    public async Task<RecipeDraftDto> ImportFromUrlAsync(string url, Household household)
    {
        var client = _httpClientFactory.CreateClient();
        var html = await client.GetStringAsync(url);
        return await ExtractDraftFromHtmlAsync(html, url, household);
    }

    public async Task<RecipeDraftDto> ExtractDraftFromHtmlAsync(string html, string url, Household household)
    {
        var jsonLdDraft = TryParseJsonLd(html);
        if (jsonLdDraft != null)
        {
            return jsonLdDraft with { ConfidenceScore = 0.8 };
        }

        if (HasAiSettings(household))
        {
            return await _aiImportService.ImportAsync(
                household.AiProvider!,
                household.AiModel!,
                household.AiApiKeyEncrypted!,
                url);
        }

        var heuristicDraft = TryParseHeuristics(html);
        return heuristicDraft with
        {
            ConfidenceScore = 0.4,
            Warnings = new List<string> { "JSON-LD not found; used heuristic extraction." }
        };
    }

    private static bool HasAiSettings(Household household)
    {
        return !string.IsNullOrWhiteSpace(household.AiProvider) &&
               !string.IsNullOrWhiteSpace(household.AiModel) &&
               !string.IsNullOrWhiteSpace(household.AiApiKeyEncrypted);
    }

    private RecipeDraftDto? TryParseJsonLd(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var nodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (nodes == null) return null;

        foreach (var node in nodes)
        {
            var json = node.InnerText;
            try
            {
                using var parsed = JsonDocument.Parse(json);
                var root = parsed.RootElement;
                var recipeElement = FindRecipeElement(root);
                if (recipeElement == null) continue;

                var recipe = recipeElement.Value;
                var title = recipe.TryGetProperty("name", out var name) ? name.GetString() : null;
                if (string.IsNullOrWhiteSpace(title)) title = "Imported Recipe";

                var description = recipe.TryGetProperty("description", out var desc) ? desc.GetString() : null;
                var ingredients = ParseIngredients(recipe);
                var steps = ParseSteps(recipe);
                var tags = ParseTags(recipe);

                return new RecipeDraftDto(
                    title,
                    description,
                    TryParseServings(recipe),
                    TryParseMinutes(recipe, "prepTime"),
                    TryParseMinutes(recipe, "cookTime"),
                    ingredients,
                    steps,
                    tags,
                    null,
                    new List<string>()
                );
            }
            catch
            {
                // Try next JSON-LD block
            }
        }

        return null;
    }

    private static List<IngredientDto> ParseIngredients(JsonElement recipe)
    {
        var ingredients = new List<IngredientDto>();
        if (!recipe.TryGetProperty("recipeIngredient", out var ing)) return ingredients;

        if (ing.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ing.EnumerateArray())
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ingredients.Add(new IngredientDto(text, null, null, null));
                }
            }
        }
        else if (ing.ValueKind == JsonValueKind.String)
        {
            var text = ing.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                ingredients.Add(new IngredientDto(text, null, null, null));
            }
        }

        return ingredients;
    }

    private static List<StepDto> ParseSteps(JsonElement recipe)
    {
        var steps = new List<StepDto>();
        if (!recipe.TryGetProperty("recipeInstructions", out var instr)) return steps;

        if (instr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in instr.EnumerateArray())
            {
                var text = ExtractInstructionText(item);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    steps.Add(new StepDto(text, null));
                }
            }
        }
        else
        {
            var text = ExtractInstructionText(instr);
            if (!string.IsNullOrWhiteSpace(text))
            {
                steps.Add(new StepDto(text, null));
            }
        }

        return steps;
    }

    private static string? ExtractInstructionText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("text", out var text))
            {
                return text.GetString();
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static List<string> ParseTags(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("keywords", out var keywords)) return new List<string>();

        var text = keywords.GetString();
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();
    }

    private static JsonElement? FindRecipeElement(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (IsRecipeType(root)) return root;

            if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in graph.EnumerateArray())
                {
                    if (IsRecipeType(item)) return item;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (IsRecipeType(item)) return item;
            }
        }

        return null;
    }

    private static bool IsRecipeType(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type)) return false;
        return type.ValueKind == JsonValueKind.String && type.GetString() == "Recipe";
    }

    private static RecipeDraftDto TryParseHeuristics(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var ingredients = new List<IngredientDto>();
        var ingredientNodes = doc.DocumentNode.SelectNodes("//*[contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'ingredient')]//li");
        if (ingredientNodes != null)
        {
            foreach (var node in ingredientNodes)
            {
                var text = node.InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ingredients.Add(new IngredientDto(text, null, null, null));
                }
            }
        }

        var steps = new List<StepDto>();
        var stepNodes = doc.DocumentNode.SelectNodes("//*[contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'instruction') or contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'direction')]//li");
        if (stepNodes != null)
        {
            foreach (var node in stepNodes)
            {
                var text = node.InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    steps.Add(new StepDto(text, null));
                }
            }
        }

        return new RecipeDraftDto(
            "Imported Recipe",
            null,
            null,
            null,
            null,
            ingredients,
            steps,
            new List<string>(),
            null,
            new List<string>()
        );
    }

    private static int? TryParseServings(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("recipeYield", out var yield)) return null;
        var text = yield.GetString() ?? string.Empty;
        var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var result) ? result : null;
    }

    private static int? TryParseMinutes(JsonElement recipe, string property)
    {
        if (!recipe.TryGetProperty(property, out var value)) return null;
        var text = value.GetString() ?? string.Empty;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var result) ? result : null;
    }
}
