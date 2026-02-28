using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Services;

public class RecipeNutritionService
{
    private readonly HouseholdAiSettingsService _aiSettings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiDebugLogService? _debugLogService;

    public RecipeNutritionService(
        HouseholdAiSettingsService aiSettings,
        IHttpClientFactory httpClientFactory,
        AiDebugLogService? debugLogService)
    {
        _aiSettings = aiSettings;
        _httpClientFactory = httpClientFactory;
        _debugLogService = debugLogService;
    }

    public async Task<NutritionEstimateSnapshot> EstimateAsync(
        Recipe recipe,
        Household household,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var provider = household.AiProvider?.Trim();
        var model = household.AiModel?.Trim();

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(household.AiApiKeyEncrypted))
        {
            throw new InvalidOperationException("Household AI settings are incomplete. Please configure provider, model, and API key in Household Settings.");
        }

        var apiKey = _aiSettings.Decrypt(household.AiApiKeyEncrypted).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Household AI API key is empty. Please update it in Household Settings.");
        }

        var prompt = BuildPrompt(recipe);
        var client = _httpClientFactory.CreateClient();

        return provider switch
        {
            "OpenAI" => await EstimateWithOpenAiAsync(client, model, apiKey, household.Id, userId, prompt, cancellationToken),
            "Anthropic" => await EstimateWithAnthropicAsync(client, model, apiKey, household.Id, userId, prompt, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported AI provider '{provider}'.")
        };
    }

    private async Task<NutritionEstimateSnapshot> EstimateWithOpenAiAsync(
        HttpClient client,
        string model,
        string apiKey,
        Guid householdId,
        Guid userId,
        string prompt,
        CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.2
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var response = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogAsync(householdId, userId, "OpenAI", model, payloadJson, body, (int)response.StatusCode, false, "OpenAI nutrition estimate failed");
            throw new InvalidOperationException("AI nutrition estimation failed.");
        }

        await LogAsync(householdId, userId, "OpenAI", model, payloadJson, body, (int)response.StatusCode, true, null);
        var content = AiResponseParser.ExtractOpenAiMessageContent(body);
        return NutritionEstimateParser.Parse(content);
    }

    private async Task<NutritionEstimateSnapshot> EstimateWithAnthropicAsync(
        HttpClient client,
        string model,
        string apiKey,
        Guid householdId,
        Guid userId,
        string prompt,
        CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var payload = new
        {
            model,
            max_tokens = 1200,
            messages = new object[]
            {
                new { role = "user", content = prompt }
            }
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogAsync(householdId, userId, "Anthropic", model, payloadJson, body, (int)response.StatusCode, false, "Anthropic nutrition estimate failed");
            throw new InvalidOperationException("AI nutrition estimation failed.");
        }

        await LogAsync(householdId, userId, "Anthropic", model, payloadJson, body, (int)response.StatusCode, true, null);
        var content = AiResponseParser.ExtractAnthropicMessageText(body);
        return NutritionEstimateParser.Parse(content);
    }

    private Task LogAsync(
        Guid householdId,
        Guid userId,
        string provider,
        string model,
        string payloadJson,
        string responseBody,
        int statusCode,
        bool success,
        string? error)
    {
        if (_debugLogService == null)
        {
            return Task.CompletedTask;
        }

        return _debugLogService.LogAsync(
            householdId,
            userId,
            provider,
            model,
            AiOperation.NutritionEstimate,
            payloadJson,
            responseBody,
            statusCode,
            success,
            error);
    }

    private static string BuildPrompt(Recipe recipe)
    {
        var servings = recipe.Servings is > 0
            ? recipe.Servings.Value.ToString(CultureInfo.InvariantCulture)
            : "unknown";

        var ingredientLines = recipe.Ingredients
            .OrderBy(i => i.OrderIndex)
            .Select(i =>
            {
                var quantity = string.IsNullOrWhiteSpace(i.Quantity) ? string.Empty : i.Quantity.Trim();
                var unit = string.IsNullOrWhiteSpace(i.Unit) ? string.Empty : i.Unit.Trim();
                var notes = string.IsNullOrWhiteSpace(i.Notes) ? string.Empty : $" ({i.Notes.Trim()})";
                return $"- {quantity} {unit} {i.Name}{notes}".Trim();
            });

        return $"""
Estimate nutrition for this recipe and return JSON only.

Recipe title: {recipe.Title}
Servings: {servings}
Ingredients:
{string.Join("\n", ingredientLines)}

Output schema:
{{
  "perServing": {{ "calories": number, "protein": number, "carbs": number, "fat": number, "fiber": number|null, "sugar": number|null, "sodiumMg": number|null }},
  "total": {{ "calories": number, "protein": number, "carbs": number, "fat": number, "fiber": number|null, "sugar": number|null, "sodiumMg": number|null }},
  "notes": "short plain text"
}}

Rules:
- use decimal numbers where appropriate
- never return negative values
- if data is unknown use null for optional micronutrients
- return JSON only without markdown fences
""";
    }
}
