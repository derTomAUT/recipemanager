using System.ComponentModel.DataAnnotations;

namespace RecipeManager.Api.DTOs;

// Request DTOs
public record CreateRecipeRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    string? Description,
    int? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    List<IngredientDto> Ingredients,
    List<StepDto> Steps,
    List<string> Tags,
    List<ImportedImageDto>? ImportedImages = null
);

public record UpdateRecipeRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    string? Description,
    int? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    List<IngredientDto> Ingredients,
    List<StepDto> Steps,
    List<string> Tags
);

public record IngredientDto(
    string Name,
    string? Quantity,
    string? Unit,
    string? Notes
);

public record StepDto(
    string Instruction,
    int? TimerSeconds
);

// Response DTOs
public record RecipeListItemDto(
    Guid Id,
    string Title,
    string? Description,
    int? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    string? TitleImageUrl,
    List<string> Tags,
    int CookCount,
    DateTime? LastCooked,
    DateTime CreatedAt
);

public record RecipeDetailDto(
    Guid Id,
    string Title,
    string? Description,
    int? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    List<RecipeIngredientDto> Ingredients,
    List<RecipeStepDto> Steps,
    List<RecipeImageDto> Images,
    List<string> Tags,
    int CookCount,
    DateTime? LastCooked,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid CreatedByUserId
);

public record RecipeIngredientDto(
    Guid Id,
    int OrderIndex,
    string Name,
    string? Quantity,
    string? Unit,
    string? Notes
);

public record RecipeStepDto(
    Guid Id,
    int OrderIndex,
    string Instruction,
    int? TimerSeconds
);

public record RecipeImageDto(
    Guid Id,
    string Url,
    bool IsTitleImage,
    int OrderIndex
);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record SetTitleImageRequest(Guid ImageId);
