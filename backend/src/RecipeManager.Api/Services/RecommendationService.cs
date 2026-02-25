using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Services;

public class RecommendationService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public RecommendationService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<Recipe>> GetRecommendedRecipesAsync(Guid userId, Guid householdId, int count = 10)
    {
        var cacheKey = $"recommendations_{userId}";

        if (_cache.TryGetValue<List<Recipe>>(cacheKey, out var cached))
        {
            return cached!;
        }

        // Get user preferences
        var prefs = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        var allergens = prefs?.Allergens ?? Array.Empty<string>();
        var disliked = prefs?.DislikedIngredients ?? Array.Empty<string>();
        var favCuisines = prefs?.FavoriteCuisines ?? Array.Empty<string>();

        // Get all recipes with related data
        var recipes = await _db.Recipes
            .Where(r => r.HouseholdId == householdId)
            .Include(r => r.Ingredients)
            .Include(r => r.Tags)
            .Include(r => r.Images)
            .ToListAsync();

        // Get cook events for scoring
        var cookCounts = await _db.CookEvents
            .Where(c => c.Recipe.HouseholdId == householdId)
            .GroupBy(c => c.RecipeId)
            .Select(g => new { RecipeId = g.Key, Count = g.Count(), LastCooked = g.Max(c => c.CookedAt) })
            .ToDictionaryAsync(x => x.RecipeId, x => (x.Count, x.LastCooked));

        // Score and filter recipes
        var scored = recipes
            .Where(r => !HasAllergen(r, allergens))  // Hard exclude allergens
            .Select(r => new
            {
                Recipe = r,
                Score = CalculateScore(r, disliked, favCuisines, cookCounts)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => Guid.NewGuid())  // Random tie-breaker
            .Take(count)
            .Select(x => x.Recipe)
            .ToList();

        _cache.Set(cacheKey, scored, TimeSpan.FromMinutes(5));
        return scored;
    }

    private bool HasAllergen(Recipe recipe, string[] allergens)
    {
        if (allergens.Length == 0) return false;
        return recipe.Ingredients.Any(i =>
            allergens.Any(a => i.Name.Contains(a, StringComparison.OrdinalIgnoreCase)));
    }

    private double CalculateScore(Recipe recipe, string[] disliked, string[] favCuisines,
        Dictionary<Guid, (int Count, DateTime LastCooked)> cookCounts)
    {
        double score = 100;  // Base score

        // Downrank disliked ingredients (-10 per match)
        var dislikedCount = recipe.Ingredients.Count(i =>
            disliked.Any(d => i.Name.Contains(d, StringComparison.OrdinalIgnoreCase)));
        score -= dislikedCount * 10;

        // Uprank favorite cuisines (+20 per match)
        var cuisineMatches = recipe.Tags.Count(t =>
            favCuisines.Any(c => t.Tag.Contains(c, StringComparison.OrdinalIgnoreCase)));
        score += cuisineMatches * 20;

        // Uprank not recently cooked (+30 if never cooked, +15 if > 14 days ago)
        if (cookCounts.TryGetValue(recipe.Id, out var cookData))
        {
            var daysSinceCooked = (DateTime.UtcNow - cookData.LastCooked).Days;
            if (daysSinceCooked > 14) score += 15;
        }
        else
        {
            score += 30;  // Never cooked
        }

        return score;
    }
}
