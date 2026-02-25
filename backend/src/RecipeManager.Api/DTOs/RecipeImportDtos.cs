namespace RecipeManager.Api.DTOs;

public record ImportRecipeUrlRequest(string Url);

public record ImportedImageDto(
    string Url,
    bool IsTitleImage,
    int OrderIndex
);

public record RecipeDraftDto(
    string Title,
    string? Description,
    int? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    List<IngredientDto> Ingredients,
    List<StepDto> Steps,
    List<string> Tags,
    List<ImportedImageDto> ImportedImages,
    double? ConfidenceScore,
    List<string> Warnings
);
