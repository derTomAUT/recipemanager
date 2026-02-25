using Microsoft.AspNetCore.DataProtection;
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
        var aiImport = new AiRecipeImportService(new TestHttpClientFactory(), aiSettings);
        var service = new RecipeImportService(new TestHttpClientFactory(), aiImport);

        var household = new Household();
        var draft = await service.ExtractDraftFromHtmlAsync(html, "https://example.com", household);

        Assert.Equal("Test Cake", draft.Title);
        Assert.Single(draft.Ingredients);
        Assert.Equal(2, draft.Steps.Count);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string? name = null) => new();
    }
}
