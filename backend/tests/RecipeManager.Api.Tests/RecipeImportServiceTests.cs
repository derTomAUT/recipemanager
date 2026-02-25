using RecipeManager.Api.Services;
using Xunit;

public class RecipeImportServiceTests
{
    [Fact]
    public void ExtractDraftFromHtml_ParsesJsonLdRecipe()
    {
        var html = @"
<html><head>
<script type=""application/ld+json"">
{""@context"":""https://schema.org"",""@type"":""Recipe"",""name"":""Test Cake"",""recipeIngredient"":[""1 cup flour""],""recipeInstructions"":[""Mix"",""Bake""],""recipeYield"":""4 servings""}
</script>
</head><body></body></html>";

        var service = new RecipeImportService(null!); // HttpClient not needed for extraction
        var draft = service.ExtractDraftFromHtml(html, "https://example.com");

        Assert.Equal("Test Cake", draft.Title);
        Assert.Single(draft.Ingredients);
        Assert.Equal(2, draft.Steps.Count);
    }
}
