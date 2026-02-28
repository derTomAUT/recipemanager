namespace RecipeManager.Api.Models;

public class PaperCardImportDraft
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Title { get; set; } = "Imported Paper Card Recipe";
    public string? Description { get; set; }
    public string? NutritionJson { get; set; }
    public string IngredientsByServingsJson { get; set; } = "{}";
    public string StepsJson { get; set; } = "[]";
    public string HeroImageUrl { get; set; } = string.Empty;
    public string StepImageUrlsJson { get; set; } = "[]";
    public string WarningsJson { get; set; } = "[]";
    public string? ConfidenceJson { get; set; }
    public string? RawExtractedTextFront { get; set; }
    public string? RawExtractedTextBack { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(2);
    public bool IsCommitted { get; set; }
}
