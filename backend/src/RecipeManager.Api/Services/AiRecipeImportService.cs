using System.Net.Http.Json;
using System.Text.Json;
using RecipeManager.Api.DTOs;

namespace RecipeManager.Api.Services;

public class AiRecipeImportService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HouseholdAiSettingsService _settings;

    public AiRecipeImportService(IHttpClientFactory httpClientFactory, HouseholdAiSettingsService settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
    }

    public async Task<RecipeDraftDto> ImportAsync(string provider, string model, string encryptedKey, string url)
    {
        var apiKey = _settings.Decrypt(encryptedKey);
        var client = _httpClientFactory.CreateClient();

        var prompt = "Extract a recipe from this URL and return a single JSON object with fields: " +
                     "title, description, servings, prepMinutes, cookMinutes, " +
                     "ingredients (array of { name, quantity, unit, notes }), " +
                     "steps (array of { instruction, timerSeconds }), " +
                     "tags (array of strings). " +
                     $"URL: {url}";

        if (provider == "OpenAI")
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            var payload = new
            {
                model,
                messages = new[] { new { role = "user", content = prompt } },
                response_format = new { type = "json_object" }
            };

            var response = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", payload);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return ParseDraftFromJson(content);
        }

        if (provider == "Anthropic")
        {
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            var payload = new
            {
                model,
                max_tokens = 2048,
                messages = new[] { new { role = "user", content = $"{prompt}\n\nReturn JSON only." } }
            };

            var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", payload);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var content = json.GetProperty("content")[0].GetProperty("text").GetString();
            return ParseDraftFromJson(content);
        }

        throw new InvalidOperationException("Unsupported AI provider");
    }

    private static RecipeDraftDto ParseDraftFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Empty AI response");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("AI response was not a JSON object");
        }

        var ingredients = new List<IngredientDto>();
        if (root.TryGetProperty("ingredients", out var ingEl) && ingEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ingEl.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                ingredients.Add(new IngredientDto(
                    name,
                    item.TryGetProperty("quantity", out var q) ? q.GetString() : null,
                    item.TryGetProperty("unit", out var u) ? u.GetString() : null,
                    item.TryGetProperty("notes", out var notes) ? notes.GetString() : null
                ));
            }
        }

        var steps = new List<StepDto>();
        if (root.TryGetProperty("steps", out var stepEl) && stepEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in stepEl.EnumerateArray())
            {
                var instruction = item.TryGetProperty("instruction", out var i) ? i.GetString() : null;
                if (string.IsNullOrWhiteSpace(instruction)) continue;

                int? timerSeconds = null;
                if (item.TryGetProperty("timerSeconds", out var t) && t.ValueKind == JsonValueKind.Number)
                {
                    timerSeconds = t.GetInt32();
                }

                steps.Add(new StepDto(instruction, timerSeconds));
            }
        }

        var tags = new List<string>();
        if (root.TryGetProperty("tags", out var tagEl) && tagEl.ValueKind == JsonValueKind.Array)
        {
            tags = tagEl.EnumerateArray()
                .Select(t => t.GetString() ?? string.Empty)
                .Where(t => t.Length > 0)
                .ToList();
        }

        return new RecipeDraftDto(
            root.TryGetProperty("title", out var title) ? title.GetString() ?? "Imported Recipe" : "Imported Recipe",
            root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
            root.TryGetProperty("servings", out var serv) && serv.ValueKind == JsonValueKind.Number ? serv.GetInt32() : null,
            root.TryGetProperty("prepMinutes", out var prep) && prep.ValueKind == JsonValueKind.Number ? prep.GetInt32() : null,
            root.TryGetProperty("cookMinutes", out var cook) && cook.ValueKind == JsonValueKind.Number ? cook.GetInt32() : null,
            ingredients,
            steps,
            tags,
            0.6,
            new List<string> { "Imported with AI" }
        );
    }
}
