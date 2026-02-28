using System;
using System.Collections.Generic;
using System.Linq;
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
}
