namespace RecipeManager.Api.Models;

public class UserPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string[] Allergens { get; set; } = Array.Empty<string>();
    public string[] DislikedIngredients { get; set; } = Array.Empty<string>();
    public string[] FavoriteCuisines { get; set; } = Array.Empty<string>();
    public User User { get; set; } = null!;
}

public class FavoriteRecipe
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RecipeId { get; set; }
    public DateTime FavoritedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Recipe Recipe { get; set; } = null!;
}
