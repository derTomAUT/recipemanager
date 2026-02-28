using System.Text.Json;

namespace RecipeManager.Api.Services;

public class AiModelCatalogService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HouseholdAiSettingsService _settings;
    private readonly AiDebugLogService _debugLogService;
    private readonly ILogger<AiModelCatalogService> _logger;

    public AiModelCatalogService(
        IHttpClientFactory factory,
        HouseholdAiSettingsService settings,
        AiDebugLogService debugLogService,
        ILogger<AiModelCatalogService> logger)
    {
        _httpClientFactory = factory;
        _settings = settings;
        _debugLogService = debugLogService;
        _logger = logger;
    }

    public async Task<List<string>> GetModelsAsync(string provider, string encryptedKey, Guid? householdId = null, Guid? userId = null)
    {
        var apiKey = _settings.Decrypt(encryptedKey).Trim();
        var client = _httpClientFactory.CreateClient();

        if (provider == "OpenAI")
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            var requestPayload = JsonSerializer.Serialize(new { endpoint = "/v1/models", provider });
            var response = await client.GetAsync("https://api.openai.com/v1/models");
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI models request failed: {Status} {Body}", response.StatusCode, body);
                await _debugLogService.LogAsync(householdId, userId, provider, "-", AiOperation.ModelList, requestPayload, body, (int)response.StatusCode, false, "OpenAI models request failed");
                response.EnsureSuccessStatusCode();
            }
            await _debugLogService.LogAsync(householdId, userId, provider, "-", AiOperation.ModelList, requestPayload, body, (int)response.StatusCode, true, null);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("data")
                .EnumerateArray()
                .Select(m => m.GetProperty("id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .OrderBy(id => id)
                .ToList();
        }

        if (provider == "Anthropic")
        {
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            var requestPayload = JsonSerializer.Serialize(new { endpoint = "/v1/models", provider });
            var response = await client.GetAsync("https://api.anthropic.com/v1/models");
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic models request failed: {Status} {Body}", response.StatusCode, body);
                await _debugLogService.LogAsync(householdId, userId, provider, "-", AiOperation.ModelList, requestPayload, body, (int)response.StatusCode, false, "Anthropic models request failed");
                response.EnsureSuccessStatusCode();
            }
            await _debugLogService.LogAsync(householdId, userId, provider, "-", AiOperation.ModelList, requestPayload, body, (int)response.StatusCode, true, null);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("data")
                .EnumerateArray()
                .Select(m => m.GetProperty("id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .OrderBy(id => id)
                .ToList();
        }

        return new List<string>();
    }
}
