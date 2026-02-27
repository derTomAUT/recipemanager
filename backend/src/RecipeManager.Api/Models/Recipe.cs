namespace RecipeManager.Api.Models;

public class Recipe
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceUrl { get; set; }
    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public Household Household { get; set; } = null!;
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
    public ICollection<RecipeImage> Images { get; set; } = new List<RecipeImage>();
    public ICollection<RecipeTag> Tags { get; set; } = new List<RecipeTag>();
}

public class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int OrderIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public Recipe Recipe { get; set; } = null!;
}

public class RecipeStep
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int OrderIndex { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public int? TimerSeconds { get; set; }
    public Recipe Recipe { get; set; } = null!;
}

public class RecipeImage
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool IsTitleImage { get; set; }
    public int OrderIndex { get; set; }
    public Recipe Recipe { get; set; } = null!;
}

public class RecipeTag
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public Recipe Recipe { get; set; } = null!;
}
