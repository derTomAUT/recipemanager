namespace RecipeManager.Api.DTOs;

public record ImportRecipeUrlRequest(string Url);

public record RecipeDraftDto(
    string Title,
    string? Description,
    int? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    List<IngredientDto> Ingredients,
    List<StepDto> Steps,
    List<string> Tags,
    double? ConfidenceScore,
    List<string> Warnings
);
