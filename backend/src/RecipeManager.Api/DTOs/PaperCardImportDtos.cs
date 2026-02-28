using System.ComponentModel.DataAnnotations;

namespace RecipeManager.Api.DTOs;

public record PaperCardParseResponseDto(
    Guid DraftId,
    string Title,
    string? Description,
    Dictionary<int, List<IngredientDto>> IngredientsByServings,
    List<int> ServingsAvailable,
    List<StepDto> Steps,
    List<ImportedImageDto> ImportedImages,
    double? ConfidenceScore,
    List<string> Warnings
);

public record CommitPaperCardImportRequest(
    [Required] Guid DraftId,
    [Required] int SelectedServings,
    string? Title,
    string? Description,
    List<IngredientDto>? Ingredients,
    List<StepDto>? Steps,
    List<string>? Tags,
    int? PrepMinutes,
    int? CookMinutes
);

public record CommitPaperCardImportResponse(Guid RecipeId);
