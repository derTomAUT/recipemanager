using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Services;

public record SeasonInfo(string Season, string Hemisphere, string Month, bool HasLocation);

public record FallbackSuggestion(
    Guid RecipeId,
    string Title,
    string Reason,
    string? Warning,
    string? TitleImageUrl
);

public class MealAssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _db;
    private readonly HouseholdAiSettingsService _aiSettings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiDebugLogService? _debugLogService;
    private readonly ILogger<MealAssistantService> _logger;

    public MealAssistantService(
        AppDbContext db,
        HouseholdAiSettingsService aiSettings,
        IHttpClientFactory httpClientFactory,
        AiDebugLogService? debugLogService,
        ILogger<MealAssistantService> logger)
    {
        _db = db;
        _aiSettings = aiSettings;
        _httpClientFactory = httpClientFactory;
        _debugLogService = debugLogService;
        _logger = logger;
    }

    public async Task<MealAssistantResponseDto> SuggestMealsAsync(
        Guid userId,
        Household household,
        string prompt,
        CancellationToken cancellationToken)
    {
        var prefs = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var allergens = prefs?.Allergens ?? Array.Empty<string>();
        var disliked = prefs?.DislikedIngredients ?? Array.Empty<string>();
        var favoriteCuisines = prefs?.FavoriteCuisines ?? Array.Empty<string>();
        var season = ResolveSeason(household.Latitude, DateTime.UtcNow);
        var warnings = new List<string>();
        if (!season.HasLocation)
        {
            warnings.Add("Household location is not configured, using neutral season defaults.");
        }

        var recipes = await _db.Recipes
            .Where(r => r.HouseholdId == household.Id)
            .Include(r => r.Ingredients)
            .Include(r => r.Tags)
            .Include(r => r.Images)
            .ToListAsync(cancellationToken);

        if (recipes.Count == 0)
        {
            return new MealAssistantResponseDto(
                season.Season,
                season.Hemisphere,
                season.Month,
                false,
                new List<string> { "No recipes available in this household yet." },
                new List<MealAssistantSuggestionDto>());
        }

        var fallbackTop3 = BuildFallbackSuggestions(
            recipes,
            allergens,
            disliked,
            favoriteCuisines,
            prompt,
            season.Season,
            3);

        if (fallbackTop3.Count == 0)
        {
            return new MealAssistantResponseDto(
                season.Season,
                season.Hemisphere,
                season.Month,
                false,
                new List<string> { "No eligible recipes found after applying allergen filters." },
                new List<MealAssistantSuggestionDto>());
        }

        var rankedCandidates = BuildFallbackSuggestions(
            recipes,
            allergens,
            disliked,
            favoriteCuisines,
            prompt,
            season.Season,
            20);

        var candidateLookup = rankedCandidates.ToDictionary(c => c.RecipeId, c => c);
        var usedAi = false;
        var selected = new List<FallbackSuggestion>();

        if (HasAiSettings(household))
        {
            try
            {
                var aiSelected = await SelectWithAiAsync(household, userId, prompt, season, rankedCandidates, cancellationToken);
                foreach (var item in aiSelected)
                {
                    if (candidateLookup.TryGetValue(item.RecipeId, out var candidate) &&
                        selected.All(s => s.RecipeId != candidate.RecipeId))
                    {
                        selected.Add(new FallbackSuggestion(
                            candidate.RecipeId,
                            candidate.Title,
                            string.IsNullOrWhiteSpace(item.Reason) ? candidate.Reason : item.Reason,
                            candidate.Warning,
                            candidate.TitleImageUrl));
                    }
                }

                if (selected.Count > 0)
                {
                    usedAi = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Meal assistant AI call failed, using deterministic fallback.");
                warnings.Add("AI meal ranking failed; showing deterministic suggestions.");
            }
        }
        else
        {
            warnings.Add("Household AI settings are incomplete; showing deterministic suggestions.");
        }

        foreach (var candidate in fallbackTop3)
        {
            if (selected.Count >= 3) break;
            if (selected.All(s => s.RecipeId != candidate.RecipeId))
            {
                selected.Add(candidate);
            }
        }

        var suggestions = selected
            .Take(3)
            .Select(s => new MealAssistantSuggestionDto(s.RecipeId, s.Title, s.Reason, s.Warning, s.TitleImageUrl))
            .ToList();

        return new MealAssistantResponseDto(
            season.Season,
            season.Hemisphere,
            season.Month,
            usedAi,
            warnings,
            suggestions);
    }

    public static SeasonInfo ResolveSeason(double? latitude, DateTime nowUtc)
    {
        var month = nowUtc.Month;
        var monthName = nowUtc.ToString("MMMM", CultureInfo.InvariantCulture);
        if (latitude == null)
        {
            return new SeasonInfo("Unknown", "Unknown", monthName, false);
        }

        var north = latitude.Value >= 0;
        var season = month switch
        {
            12 or 1 or 2 => north ? "Winter" : "Summer",
            3 or 4 or 5 => north ? "Spring" : "Autumn",
            6 or 7 or 8 => north ? "Summer" : "Winter",
            _ => north ? "Autumn" : "Spring"
        };

        return new SeasonInfo(season, north ? "Northern" : "Southern", monthName, true);
    }

    public static List<FallbackSuggestion> BuildFallbackSuggestions(
        IEnumerable<Recipe> recipes,
        string[] allergens,
        string[] disliked,
        string[] favoriteCuisines,
        string userPrompt,
        string season,
        int maxResults)
    {
        var promptTokens = Tokenize(userPrompt);

        return recipes
            .Where(recipe => !ContainsAny(recipe, allergens))
            .Select(recipe => new
            {
                Recipe = recipe,
                Score = ScoreRecipe(recipe, disliked, favoriteCuisines, promptTokens, season),
                Warning = BuildWarning(recipe, disliked)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Recipe.Title)
            .Take(Math.Max(1, maxResults))
            .Select(x => new FallbackSuggestion(
                x.Recipe.Id,
                x.Recipe.Title,
                $"Good match for your prompt and {season.ToLowerInvariant()} season.",
                x.Warning,
                x.Recipe.Images.FirstOrDefault(i => i.IsTitleImage)?.Url
                    ?? x.Recipe.Images.OrderBy(i => i.OrderIndex).FirstOrDefault()?.Url))
            .ToList();
    }

    private async Task<List<AiCandidateSelection>> SelectWithAiAsync(
        Household household,
        Guid userId,
        string prompt,
        SeasonInfo season,
        List<FallbackSuggestion> candidates,
        CancellationToken cancellationToken)
    {
        var provider = household.AiProvider?.Trim();
        var model = household.AiModel?.Trim();
        var apiKey = _aiSettings.Decrypt(household.AiApiKeyEncrypted!).Trim();
        var client = _httpClientFactory.CreateClient();
        var candidateJson = JsonSerializer.Serialize(candidates.Select(c => new { recipeId = c.RecipeId, title = c.Title }));

        var instruction =
            "Return JSON only in this exact shape: {\"suggestions\":[{\"recipeId\":\"<guid>\",\"reason\":\"<short text>\"}]}. " +
            "Choose exactly 3 unique recipeId values from the provided candidate list only. " +
            "Respect seasonality by default unless user prompt requests otherwise.";

        if (provider == "OpenAI")
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            var payload = new
            {
                model,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content =
                            $"User prompt: {prompt}\nSeason: {season.Season} ({season.Hemisphere} hemisphere), month: {season.Month}.\n" +
                            $"Candidates: {candidateJson}\n{instruction}"
                    }
                },
                response_format = new { type = "json_object" }
            };
            var payloadJson = JsonSerializer.Serialize(payload);

            var response = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogAiDebugAsync(
                    household.Id,
                    userId,
                    provider!,
                    model!,
                    payloadJson,
                    body,
                    (int)response.StatusCode,
                    false,
                    "OpenAI meal assistant request failed");
                response.EnsureSuccessStatusCode();
            }
            await LogAiDebugAsync(household.Id, userId, provider!, model!, payloadJson, body, (int)response.StatusCode, true, null);
            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return ParseAiSuggestions(content);
        }

        if (provider == "Anthropic")
        {
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            var payload = new
            {
                model,
                max_tokens = 900,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content =
                            $"User prompt: {prompt}\nSeason: {season.Season} ({season.Hemisphere} hemisphere), month: {season.Month}.\n" +
                            $"Candidates: {candidateJson}\n{instruction}"
                    }
                }
            };
            var payloadJson = JsonSerializer.Serialize(payload);

            var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogAiDebugAsync(
                    household.Id,
                    userId,
                    provider!,
                    model!,
                    payloadJson,
                    body,
                    (int)response.StatusCode,
                    false,
                    "Anthropic meal assistant request failed");
                response.EnsureSuccessStatusCode();
            }
            await LogAiDebugAsync(household.Id, userId, provider!, model!, payloadJson, body, (int)response.StatusCode, true, null);
            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            return ParseAiSuggestions(content);
        }

        throw new InvalidOperationException($"Unsupported AI provider '{provider}'.");
    }

    private static List<AiCandidateSelection> ParseAiSuggestions(string? jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return new List<AiCandidateSelection>();
        }

        using var doc = JsonDocument.Parse(jsonContent);
        if (!doc.RootElement.TryGetProperty("suggestions", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return new List<AiCandidateSelection>();
        }

        var result = new List<AiCandidateSelection>();
        foreach (var item in list.EnumerateArray())
        {
            var idText = item.TryGetProperty("recipeId", out var idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString()
                : null;
            if (!Guid.TryParse(idText, out var recipeId))
            {
                continue;
            }

            var reason = item.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String
                ? reasonEl.GetString() ?? string.Empty
                : string.Empty;

            result.Add(new AiCandidateSelection(recipeId, reason));
        }

        return result;
    }

    private static bool HasAiSettings(Household household)
    {
        return !string.IsNullOrWhiteSpace(household.AiProvider) &&
               !string.IsNullOrWhiteSpace(household.AiModel) &&
               !string.IsNullOrWhiteSpace(household.AiApiKeyEncrypted);
    }

    private static bool ContainsAny(Recipe recipe, string[] terms)
    {
        if (terms.Length == 0) return false;
        return recipe.Ingredients.Any(i => terms.Any(term =>
            i.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private static string? BuildWarning(Recipe recipe, string[] disliked)
    {
        var matches = recipe.Ingredients
            .Select(i => i.Name)
            .Where(name => disliked.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        return matches.Count == 0 ? null : $"Contains disliked ingredient(s): {string.Join(", ", matches)}";
    }

    private static double ScoreRecipe(
        Recipe recipe,
        string[] disliked,
        string[] favoriteCuisines,
        HashSet<string> promptTokens,
        string season)
    {
        var score = 100.0;

        foreach (var ingredient in recipe.Ingredients)
        {
            if (disliked.Any(d => ingredient.Name.Contains(d, StringComparison.OrdinalIgnoreCase)))
            {
                score -= 15;
            }

            if (promptTokens.Any(token => ingredient.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                score += 9;
            }
        }

        foreach (var tag in recipe.Tags.Select(t => t.Tag))
        {
            if (favoriteCuisines.Any(c => tag.Contains(c, StringComparison.OrdinalIgnoreCase)))
            {
                score += 18;
            }

            if (promptTokens.Any(token => tag.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                score += 6;
            }
        }

        if (season == "Winter" && HasAnyTag(recipe, "bbq", "grill", "salad"))
        {
            score -= 10;
        }
        else if (season == "Summer" && HasAnyTag(recipe, "stew", "soup", "roast"))
        {
            score -= 8;
        }

        return score;
    }

    private static bool HasAnyTag(Recipe recipe, params string[] tags)
    {
        return recipe.Tags.Any(t => tags.Any(value => t.Tag.Contains(value, StringComparison.OrdinalIgnoreCase)));
    }

    private static HashSet<string> Tokenize(string input)
    {
        return input
            .Split(new[] { ' ', ',', '.', ';', ':', '/', '\\', '!', '?', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length >= 3)
            .ToHashSet();
    }

    private record AiCandidateSelection(Guid RecipeId, string Reason);

    private Task LogAiDebugAsync(
        Guid householdId,
        Guid userId,
        string provider,
        string model,
        string requestJson,
        string responseJson,
        int statusCode,
        bool success,
        string? error)
    {
        return _debugLogService == null
            ? Task.CompletedTask
            : _debugLogService.LogAsync(
                householdId,
                userId,
                provider,
                model,
                AiOperation.MealAssistant,
                requestJson,
                responseJson,
                statusCode,
                success,
                error);
    }
}
