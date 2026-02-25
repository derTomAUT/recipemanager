namespace RecipeManager.Api.Models;

public class CookEvent
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid UserId { get; set; }
    public Guid HouseholdId { get; set; }
    public DateTime CookedAt { get; set; } = DateTime.UtcNow;
    public int? Servings { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public User User { get; set; } = null!;
}
