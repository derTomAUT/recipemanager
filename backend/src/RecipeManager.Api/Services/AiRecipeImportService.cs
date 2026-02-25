using System.Net.Http.Json;
using System.Text.Json;
using RecipeManager.Api.DTOs;

namespace RecipeManager.Api.Services;

public class AiRecipeImportService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HouseholdAiSettingsService _settings;
    private readonly ILogger<AiRecipeImportService> _logger;

    public AiRecipeImportService(
        IHttpClientFactory httpClientFactory,
        HouseholdAiSettingsService settings,
        ILogger<AiRecipeImportService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<RecipeDraftDto> ImportAsync(
        string provider,
        string model,
        string encryptedKey,
        string url,
        string readableText,
        bool wasTruncated)
    {
        var apiKey = _settings.Decrypt(encryptedKey).Trim();
        var client = _httpClientFactory.CreateClient();

        var readableSection = string.IsNullOrWhiteSpace(readableText)
            ? "Readable text was unavailable."
            : $"Readable text (truncated={wasTruncated}):\n{readableText}";

        var prompt = "Read the following recipe website and extract the concrete recipe. Return a single JSON object with fields: " +
                     "title, description, servings, prepMinutes, cookMinutes, " +
                     "ingredients (array of { name, quantity, unit, notes }), " +
                     "steps (array of { instruction, timerSeconds }), " +
                     "tags (array of strings). " +
                     $"URL: {url}\n\n{readableSection}";

        _logger.LogDebug("AI Import Prompt: {prompt}", prompt);
        if (provider == "OpenAI")
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            var payload = new
            {
                model,
                messages = new[] { new { role = "user", content = prompt } },
                response_format = new { type = "json_object" }
            };
            _logger.LogDebug("OpenAI Payload: {payload}", payload);

            var payloadJson = JsonSerializer.Serialize(payload);
            _logger.LogDebug("AI import request (OpenAI): {Json}", payloadJson);
            var response = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", payload);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            _logger.LogDebug("AI import response (OpenAI): {Json}", content);
            return ParseDraftFromJson(SanitizeJson(content));
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

            var payloadJson = JsonSerializer.Serialize(payload);
            _logger.LogDebug("AI import request (Anthropic): {Json}", payloadJson);
            var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", payload);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var content = json.GetProperty("content")[0].GetProperty("text").GetString();
            _logger.LogDebug("AI import response (Anthropic): {Json}", content);
            return ParseDraftFromJson(SanitizeJson(content));
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
                var name = item.TryGetProperty("name", out var n) ? GetFlexibleString(n) : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                ingredients.Add(new IngredientDto(
                    name,
                    item.TryGetProperty("quantity", out var q) ? GetFlexibleString(q) : null,
                    item.TryGetProperty("unit", out var u) ? GetFlexibleString(u) : null,
                    item.TryGetProperty("notes", out var notes) ? GetFlexibleString(notes) : null
                ));
            }
        }

        var steps = new List<StepDto>();
        if (root.TryGetProperty("steps", out var stepEl) && stepEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in stepEl.EnumerateArray())
            {
                var instruction = item.TryGetProperty("instruction", out var i) ? GetFlexibleString(i) : null;
                if (string.IsNullOrWhiteSpace(instruction)) continue;

                int? timerSeconds = null;
                if (item.TryGetProperty("timerSeconds", out var t))
                {
                    timerSeconds = TryGetInt(t);
                }

                steps.Add(new StepDto(instruction, timerSeconds));
            }
        }

        var tags = new List<string>();
        if (root.TryGetProperty("tags", out var tagEl) && tagEl.ValueKind == JsonValueKind.Array)
        {
            tags = tagEl.EnumerateArray()
                .Select(GetFlexibleString)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .ToList();
        }

        return new RecipeDraftDto(
            root.TryGetProperty("title", out var title) ? GetFlexibleString(title) ?? "Imported Recipe" : "Imported Recipe",
            root.TryGetProperty("description", out var desc) ? GetFlexibleString(desc) : null,
            root.TryGetProperty("servings", out var serv) ? TryGetInt(serv) : null,
            root.TryGetProperty("prepMinutes", out var prep) ? TryGetInt(prep) : null,
            root.TryGetProperty("cookMinutes", out var cook) ? TryGetInt(cook) : null,
            ingredients,
            steps,
            tags,
            0.6,
            new List<string> { "Imported with AI" }
        );
    }

    private static string SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        var trimmed = json.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var startFenceEnd = trimmed.IndexOf('\n');
        if (startFenceEnd < 0)
        {
            return trimmed;
        }

        var endFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (endFence <= startFenceEnd)
        {
            return trimmed;
        }

        return trimmed.Substring(startFenceEnd + 1, endFence - startFenceEnd - 1).Trim();
    }

    private static string? GetFlexibleString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? TryGetInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out var value))
            {
                return value;
            }
            if (element.TryGetDouble(out var dbl))
            {
                return (int)Math.Round(dbl);
            }
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (int.TryParse(text, out var value))
            {
                return value;
            }
            if (double.TryParse(text, out var dbl))
            {
                return (int)Math.Round(dbl);
            }
        }

        return null;
    }
}
