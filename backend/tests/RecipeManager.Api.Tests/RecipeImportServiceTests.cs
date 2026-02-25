using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
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
        var service = new RecipeImportService(new TestHttpClientFactory(), aiImport);

        var household = new Household();
        var draft = await service.ExtractDraftFromHtmlAsync(html, "https://example.com", household);

        Assert.Equal("Test Cake", draft.Title);
        Assert.Single(draft.Ingredients);
        Assert.Equal(2, draft.Steps.Count);
    }

    [Fact]
    public void ExtractReadableText_StripsScriptAndReturnsContent()
    {
        var html = @"<html><head><style>.x{}</style><script>ignore()</script></head>
<body><header>Header</header><main><h1>Title</h1><p>Keep this text.</p></main></body></html>";

        var dataProtectionProvider = DataProtectionProvider.Create("RecipeManager.Tests");
        var aiSettings = new HouseholdAiSettingsService(dataProtectionProvider);
        var aiImport = new AiRecipeImportService(new TestHttpClientFactory(), aiSettings, NullLogger<AiRecipeImportService>.Instance);
        var service = new RecipeImportService(new TestHttpClientFactory(), aiImport);

        var text = service.ExtractReadableText(html);

        Assert.Contains("Title", text);
        Assert.Contains("Keep this text.", text);
        Assert.DoesNotContain("ignore()", text);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string? name = null) => new();
    }
}
