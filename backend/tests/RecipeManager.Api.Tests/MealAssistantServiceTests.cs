using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RecipeManager.Api.Models;
using RecipeManager.Api.Services;
using Xunit;

public class MealAssistantServiceTests
{
    [Theory]
    [InlineData(47.0, 1, "Winter")]
    [InlineData(47.0, 4, "Spring")]
    [InlineData(47.0, 7, "Summer")]
    [InlineData(47.0, 10, "Autumn")]
    [InlineData(-33.0, 1, "Summer")]
    [InlineData(-33.0, 4, "Autumn")]
    [InlineData(-33.0, 7, "Winter")]
    [InlineData(-33.0, 10, "Spring")]
    public void ResolveSeason_UsesHouseholdLatitude(double latitude, int month, string expectedSeason)
    {
        var now = new DateTime(2026, month, 15, 12, 0, 0, DateTimeKind.Utc);
        var season = MealAssistantService.ResolveSeason(latitude, now);
        Assert.Equal(expectedSeason, season.Season);
    }

    [Fact]
    public void BuildFallbackSuggestions_ExcludesAllergenMatches()
    {
        var recipes = new List<Recipe>
        {
            BuildRecipe("Peanut Stir Fry", new[] { "peanut", "chicken" }, new[] { "stir-fry" }),
            BuildRecipe("Tomato Pasta", new[] { "tomato", "basil" }, new[] { "italian" }),
            BuildRecipe("Veggie Bowl", new[] { "broccoli", "rice" }, new[] { "healthy" })
        };

        var suggestions = MealAssistantService.BuildFallbackSuggestions(
            recipes,
            allergens: new[] { "peanut" },
            disliked: Array.Empty<string>(),
            favoriteCuisines: Array.Empty<string>(),
            userPrompt: "quick dinner",
            season: "Winter",
            maxResults: 3);

        Assert.DoesNotContain(suggestions, s => s.Title.Contains("Peanut", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildFallbackSuggestions_ReturnsAtMostTopThree()
    {
        var recipes = Enumerable.Range(1, 10)
            .Select(i => BuildRecipe($"Recipe {i}", new[] { "ingredient" + i }, new[] { "tag" + i }))
            .ToList();

        var suggestions = MealAssistantService.BuildFallbackSuggestions(
            recipes,
            allergens: Array.Empty<string>(),
            disliked: Array.Empty<string>(),
            favoriteCuisines: Array.Empty<string>(),
            userPrompt: "easy",
            season: "Summer",
            maxResults: 3);

        Assert.Equal(3, suggestions.Count);
    }

    [Fact]
    public void ParseAiSuggestions_AcceptsJsonWrappedInCodeFence()
    {
        var recipeId = Guid.NewGuid();
        var payload =
            "```json\n" +
            $"{{\"suggestions\":[{{\"recipeId\":\"{recipeId}\",\"reason\":\"Comforting and warm.\"}}]}}\n" +
            "```";

        var parsed = InvokeParseAiSuggestions(payload);

        Assert.Single(parsed);
        Assert.Equal(recipeId, parsed[0].RecipeId);
        Assert.Equal("Comforting and warm.", parsed[0].Reason);
    }

    [Fact]
    public void ParseAiSuggestions_AcceptsJsonEmbeddedInText()
    {
        var recipeId = Guid.NewGuid();
        var payload =
            "Here are your picks:\n" +
            $"{{\"suggestions\":[{{\"recipeId\":\"{recipeId}\",\"reason\":\"Great for tomorrow lunch.\"}}]}}\n" +
            "Enjoy!";

        var parsed = InvokeParseAiSuggestions(payload);

        Assert.Single(parsed);
        Assert.Equal(recipeId, parsed[0].RecipeId);
    }

    private static Recipe BuildRecipe(string title, IEnumerable<string> ingredientNames, IEnumerable<string> tags)
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var ingredient in ingredientNames)
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                Name = ingredient
            });
        }

        foreach (var tag in tags)
        {
            recipe.Tags.Add(new RecipeTag
            {
                Id = Guid.NewGuid(),
                Tag = tag
            });
        }

        return recipe;
    }

    private static List<ParsedSuggestion> InvokeParseAiSuggestions(string content)
    {
        var method = typeof(MealAssistantService).GetMethod("ParseAiSuggestions", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object?[] { content });
        Assert.NotNull(result);

        var list = new List<ParsedSuggestion>();
        foreach (var item in (System.Collections.IEnumerable)result!)
        {
            var itemType = item!.GetType();
            var recipeIdValue = itemType.GetProperty("RecipeId")!.GetValue(item);
            var reasonValue = itemType.GetProperty("Reason")!.GetValue(item);
            list.Add(new ParsedSuggestion((Guid)recipeIdValue!, (string)reasonValue!));
        }

        return list;
    }

    private readonly record struct ParsedSuggestion(Guid RecipeId, string Reason);
}
