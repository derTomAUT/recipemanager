using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeManager.Api.Infrastructure.Storage;
using RecipeManager.Api.Models;
using RecipeManager.Api.Services;
using Xunit;

public class RecipeImportServiceTests
{
    [Fact]
    public async Task ExtractDraftFromHtml_ParsesJsonLdRecipe()
    {
        var html = @"
<html><head>
<script type=""application/ld+json"">
{""@context"":""https://schema.org"",""@type"":""Recipe"",""name"":""Test Cake"",""recipeIngredient"":[""1 cup flour""],""recipeInstructions"":[""Mix"",""Bake""],""recipeYield"":""4 servings""}
</script>
</head><body></body></html>";

        var dataProtectionProvider = DataProtectionProvider.Create("RecipeManager.Tests");
        var aiSettings = new HouseholdAiSettingsService(dataProtectionProvider);
        var aiImport = new AiRecipeImportService(new TestHttpClientFactory(), aiSettings, NullLogger<AiRecipeImportService>.Instance);
        var service = new RecipeImportService(
            new TestHttpClientFactory(),
            aiImport,
            new ImageFetchService(new TestHttpClientFactory()),
            new TestStorageService());

        var household = new Household();
        var draft = await service.ExtractDraftFromHtmlAsync(html, "https://example.com", household);

        Assert.Equal("Test Cake", draft.Title);
        Assert.Single(draft.Ingredients);
        Assert.Equal(2, draft.Steps.Count);
    }

    [Fact]
    public async Task ExtractDraftFromHtml_IncludesSourceUrl()
    {
        var html = @"
<html><head>
<script type=""application/ld+json"">
{""@context"":""https://schema.org"",""@type"":""Recipe"",""name"":""Test Cake""}
</script>
</head><body></body></html>";

        var dataProtectionProvider = DataProtectionProvider.Create("RecipeManager.Tests");
        var aiSettings = new HouseholdAiSettingsService(dataProtectionProvider);
        var aiImport = new AiRecipeImportService(new TestHttpClientFactory(), aiSettings, NullLogger<AiRecipeImportService>.Instance);
        var service = new RecipeImportService(
            new TestHttpClientFactory(),
            aiImport,
            new ImageFetchService(new TestHttpClientFactory()),
            new TestStorageService());

        var household = new Household();
        var url = "https://example.com/recipe";

        var draft = await service.ExtractDraftFromHtmlAsync(html, url, household);

        Assert.Equal(url, draft.SourceUrl);
    }

    [Fact]
    public void ExtractReadableText_StripsScriptAndReturnsContent()
    {
        var html = @"<html><head><style>.x{}</style><script>ignore()</script></head>
<body><header>Header</header><main><h1>Title</h1><p>Keep this text.</p></main></body></html>";

        var dataProtectionProvider = DataProtectionProvider.Create("RecipeManager.Tests");
        var aiSettings = new HouseholdAiSettingsService(dataProtectionProvider);
        var aiImport = new AiRecipeImportService(new TestHttpClientFactory(), aiSettings, NullLogger<AiRecipeImportService>.Instance);
        var service = new RecipeImportService(
            new TestHttpClientFactory(),
            aiImport,
            new ImageFetchService(new TestHttpClientFactory()),
            new TestStorageService());

        var text = service.ExtractReadableText(html);

        Assert.Contains("Title", text);
        Assert.Contains("Keep this text.", text);
        Assert.DoesNotContain("ignore()", text);
    }

    [Fact]
    public async Task ImportFromUrlAsync_AddsBrowserHeaders()
    {
        var handler = new RecordingHandler();
        var client = new HttpClient(handler);
        var factory = new RecordingHttpClientFactory(client);

        var dataProtectionProvider = DataProtectionProvider.Create("RecipeManager.Tests");
        var aiSettings = new HouseholdAiSettingsService(dataProtectionProvider);
        var aiImport = new AiRecipeImportService(factory, aiSettings, NullLogger<AiRecipeImportService>.Instance);
        var service = new RecipeImportService(
            factory,
            aiImport,
            new ImageFetchService(factory),
            new TestStorageService());

        var household = new Household();
        await service.ImportFromUrlAsync("https://example.com/recipe", household);

        Assert.NotNull(handler.Request);
        Assert.True(handler.Request!.Headers.UserAgent.Count > 0);
        Assert.True(handler.Request.Headers.Accept.Count > 0);
        Assert.True(handler.Request.Headers.AcceptLanguage.Count > 0);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string? name = null) => new();
    }

    private sealed class TestStorageService : IStorageService
    {
        public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
            => Task.FromResult($"/uploads/{fileName}");

        public Task DeleteAsync(string url) => Task.CompletedTask;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            });
        }
    }

    private sealed class RecordingHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public RecordingHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string? name = null) => _client;
    }
}
