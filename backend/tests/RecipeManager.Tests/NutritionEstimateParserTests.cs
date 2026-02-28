using RecipeManager.Api.Services;

namespace RecipeManager.Tests;

public class NutritionEstimateParserTests
{
    [Fact]
    public void Parse_ValidJsonObject_ReturnsEstimate()
    {
        var json = """
        {
          "perServing": { "calories": 450, "protein": 22.5, "carbs": 40.2, "fat": 18.1, "fiber": 4.3, "sugar": 5.5, "sodiumMg": 620 },
          "total": { "calories": 1800, "protein": 90, "carbs": 160.8, "fat": 72.4, "fiber": 17.2, "sugar": 22, "sodiumMg": 2480 },
          "notes": "Estimated from common ingredients"
        }
        """;

        var result = NutritionEstimateParser.Parse(json);

        Assert.Equal(450m, result.PerServing.Calories);
        Assert.Equal(22.5m, result.PerServing.Protein);
        Assert.Equal(1800m, result.Total.Calories);
        Assert.Equal("Estimated from common ingredients", result.Notes);
    }

    [Fact]
    public void Parse_CodeFenceJson_ReturnsEstimate()
    {
        var content = """
        Here is the estimate:
        ```json
        {"perServing":{"calories":300,"protein":10,"carbs":30,"fat":12},"total":{"calories":1200,"protein":40,"carbs":120,"fat":48}}
        ```
        """;

        var result = NutritionEstimateParser.Parse(content);

        Assert.Equal(300m, result.PerServing.Calories);
        Assert.Equal(1200m, result.Total.Calories);
    }

    [Fact]
    public void Parse_MissingRequiredFields_Throws()
    {
        var invalid = "{" + "\"foo\":1" + "}";

        Assert.Throws<InvalidOperationException>(() => NutritionEstimateParser.Parse(invalid));
    }
}
