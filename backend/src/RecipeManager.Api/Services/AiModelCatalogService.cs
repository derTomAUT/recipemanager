using System.Text.Json;

namespace RecipeManager.Api.Services;

public class AiModelCatalogService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HouseholdAiSettingsService _settings;

    public AiModelCatalogService(IHttpClientFactory factory, HouseholdAiSettingsService settings)
    {
        _httpClientFactory = factory;
        _settings = settings;
    }

    public async Task<List<string>> GetModelsAsync(string provider, string encryptedKey)
    {
        var apiKey = _settings.Decrypt(encryptedKey);
        var client = _httpClientFactory.CreateClient();

        if (provider == "OpenAI")
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            var json = await client.GetStringAsync("https://api.openai.com/v1/models");
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
            var json = await client.GetStringAsync("https://api.anthropic.com/v1/models");
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
