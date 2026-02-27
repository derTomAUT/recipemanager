using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
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

    [Fact]
    public void ExtractImageCandidates_UsesLazyLoadedAttributes()
    {
        var html = @"
<html><body><main>
  <img
    src=""data:image/svg+xml,%3Csvg%3E%3C/svg%3E""
    data-jpibfi-src=""https://example.com/images/ranch-main.jpg""
    data-lazy-src=""https://example.com/images/ranch-main.jpg""
    data-lazy-srcset=""https://example.com/images/ranch-main.jpg 731w, https://example.com/images/ranch-small.jpg 214w"" />
</main></body></html>";

        var service = CreateService();
        var candidates = service.ExtractImageCandidates(html, new Uri("https://example.com/recipe"));

        Assert.Contains("https://example.com/images/ranch-main.jpg", candidates);
        Assert.DoesNotContain("https://example.com/images/ranch-small.jpg", candidates);
        Assert.DoesNotContain(candidates, c => c.StartsWith("data:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExtractImageCandidates_ResolvesRelativeSrcSetUrls()
    {
        var html = @"
<html><body><article>
  <img src=""data:image/svg+xml,%3Csvg%3E%3C/svg%3E""
       srcset=""/images/step1.jpg 600w, /images/step1-small.jpg 300w"" />
</article></body></html>";

        var service = CreateService();
        var candidates = service.ExtractImageCandidates(html, new Uri("https://example.com/recipe"));

        Assert.Contains("https://example.com/images/step1.jpg", candidates);
        Assert.DoesNotContain("https://example.com/images/step1-small.jpg", candidates);
    }

    [Fact]
    public async Task FetchImagesAsync_DeduplicatesIdenticalImageBytes()
    {
        var imageA = "https://example.com/images/a.jpg";
        var imageB = "https://example.com/images/b.jpg";
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var handler = new DuplicateImageBytesHandler(imageA, imageB, bytes);
        var client = new HttpClient(handler);
        var factory = new RecordingHttpClientFactory(client);
        var fetchService = new ImageFetchService(factory);

        var results = await fetchService.FetchImagesAsync(new[] { imageA, imageB }, pageUri: new Uri("https://example.com/recipe"));

        Assert.Single(results);
    }

    [Fact]
    public void ExtractImageCandidates_DeduplicatesWordPressSizeVariants()
    {
        var html = @"
<html><body><main>
  <img src=""https://example.com/wp-content/uploads/2026/02/ranch-dip-214x300.jpg"" />
  <img data-lazy-src=""https://example.com/wp-content/uploads/2026/02/ranch-dip-731x1024.jpg"" />
  <img data-jpibfi-src=""https://example.com/wp-content/uploads/2026/02/ranch-dip-1097x1536.jpg"" />
</main></body></html>";

        var service = CreateService();
        var candidates = service.ExtractImageCandidates(html, new Uri("https://example.com/recipe"));

        Assert.Single(candidates);
        Assert.Equal("https://example.com/wp-content/uploads/2026/02/ranch-dip-1097x1536.jpg", candidates[0]);
    }

    [Fact]
    public async Task ImportFromUrlAsync_FetchesProtectedImagesWithBrowserHeaders()
    {
        var pageUrl = "https://example.com/recipe";
        var imageUrl = "https://example.com/images/ranch.jpg";
        var handler = new ProtectedImageHandler(pageUrl, imageUrl);
        var client = new HttpClient(handler);
        var factory = new RecordingHttpClientFactory(client);

        var dataProtectionProvider = DataProtectionProvider.Create("RecipeManager.Tests");
        var aiSettings = new HouseholdAiSettingsService(dataProtectionProvider);
        var encryptedKey = aiSettings.Encrypt("test-key");
        var aiImport = new AiRecipeImportService(factory, aiSettings, NullLogger<AiRecipeImportService>.Instance);
        var service = new RecipeImportService(factory, aiImport, new ImageFetchService(factory), new TestStorageService());

        var household = new Household
        {
            AiProvider = "OpenAI",
            AiModel = "gpt-4o-mini",
            AiApiKeyEncrypted = encryptedKey
        };

        var draft = await service.ImportFromUrlAsync(pageUrl, household);

        Assert.NotEmpty(draft.CandidateImages);
    }

    private static RecipeImportService CreateService()
    {
        var dataProtectionProvider = DataProtectionProvider.Create("RecipeManager.Tests");
        var aiSettings = new HouseholdAiSettingsService(dataProtectionProvider);
        var httpFactory = new TestHttpClientFactory();
        var aiImport = new AiRecipeImportService(httpFactory, aiSettings, NullLogger<AiRecipeImportService>.Instance);
        return new RecipeImportService(
            httpFactory,
            aiImport,
            new ImageFetchService(httpFactory),
            new TestStorageService());
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

    private sealed class ProtectedImageHandler : HttpMessageHandler
    {
        private readonly string _pageUrl;
        private readonly string _imageUrl;

        public ProtectedImageHandler(string pageUrl, string imageUrl)
        {
            _pageUrl = pageUrl;
            _imageUrl = imageUrl;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUrl = request.RequestUri?.ToString() ?? string.Empty;

            if (request.Method == HttpMethod.Get && requestUrl == _pageUrl)
            {
                var html = $@"
<html><body><main>
<script type=""application/ld+json"">
{{""@context"":""https://schema.org"",""@type"":""Recipe"",""name"":""Test Recipe""}}
</script>
<img src=""{_imageUrl}"" />
</main></body></html>";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html)
                });
            }

            if (request.Method == HttpMethod.Get && requestUrl == _imageUrl)
            {
                var hasUserAgent = request.Headers.UserAgent.Count > 0;
                var hasReferrer = request.Headers.Referrer?.Host == "example.com";
                if (!hasUserAgent || !hasReferrer)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                }

                var content = new ByteArrayContent(new byte[] { 0x01, 0x02, 0x03 });
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                });
            }

            if (request.Method == HttpMethod.Post)
            {
                // Force AI selection call to fail so the import path uses non-AI fallback image selection.
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class DuplicateImageBytesHandler : HttpMessageHandler
    {
        private readonly string _imageA;
        private readonly string _imageB;
        private readonly byte[] _bytes;

        public DuplicateImageBytesHandler(string imageA, string imageB, byte[] bytes)
        {
            _imageA = imageA;
            _imageB = imageB;
            _bytes = bytes;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUrl = request.RequestUri?.ToString() ?? string.Empty;
            if (requestUrl == _imageA || requestUrl == _imageB)
            {
                var content = new ByteArrayContent(_bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
