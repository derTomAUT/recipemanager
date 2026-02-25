using System.Text.Json;

namespace RecipeManager.Api.Services;

public class AiModelCatalogService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HouseholdAiSettingsService _settings;
    private readonly ILogger<AiModelCatalogService> _logger;

    public AiModelCatalogService(
        IHttpClientFactory factory,
        HouseholdAiSettingsService settings,
        ILogger<AiModelCatalogService> logger)
    {
        _httpClientFactory = factory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<List<string>> GetModelsAsync(string provider, string encryptedKey)
    {
        var apiKey = _settings.Decrypt(encryptedKey).Trim();
        var client = _httpClientFactory.CreateClient();

        if (provider == "OpenAI")
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            var response = await client.GetAsync("https://api.openai.com/v1/models");
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI models request failed: {Status} {Body}", response.StatusCode, body);
                response.EnsureSuccessStatusCode();
            }
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
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
            var response = await client.GetAsync("https://api.anthropic.com/v1/models");
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Anthropic models request failed: {Status} {Body}", response.StatusCode, body);
                response.EnsureSuccessStatusCode();
            }
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
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
