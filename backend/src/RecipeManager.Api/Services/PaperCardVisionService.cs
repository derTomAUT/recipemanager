using System.Text.RegularExpressions;
using RecipeManager.Api.DTOs;

namespace RecipeManager.Api.Services;

public record PaperCardVisionResult(
    string Title,
    string? Description,
    Dictionary<int, List<IngredientDto>> IngredientsByServings,
    List<StepDto> Steps,
    string? RawFrontText,
    string? RawBackText,
    double? ConfidenceScore,
    List<string> Warnings
);

public interface IPaperCardVisionService
{
    Task<PaperCardVisionResult> ExtractAsync(IFormFile frontImage, IFormFile backImage, CancellationToken cancellationToken);
}

public class PaperCardVisionService : IPaperCardVisionService
{
    public Task<PaperCardVisionResult> ExtractAsync(IFormFile frontImage, IFormFile backImage, CancellationToken cancellationToken)
    {
        var title = BuildTitleFromFileName(frontImage.FileName);

        // Initial extractor fallback: keep user fully in control via edit/review before save.
        var ingredientsByServings = new Dictionary<int, List<IngredientDto>>
        {
            [2] = new List<IngredientDto>(),
            [3] = new List<IngredientDto>(),
            [4] = new List<IngredientDto>()
        };

        var steps = new List<StepDto>
        {
            new("Review extracted cooking steps from card back image and adjust as needed.", null)
        };

        var warnings = new List<string>
        {
            "Paper card OCR extraction is running in guided fallback mode. Please review title, ingredients, and steps."
        };

        var result = new PaperCardVisionResult(
            title,
            null,
            ingredientsByServings,
            steps,
            null,
            null,
            0.25,
            warnings
        );

        return Task.FromResult(result);
    }

    private static string BuildTitleFromFileName(string fileName)
    {
        var withoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(withoutExt))
        {
            return "Imported Paper Card Recipe";
        }

        var normalized = Regex.Replace(withoutExt, "[-_]+", " ").Trim();
        normalized = Regex.Replace(normalized, "\\s+", " ");
        if (normalized.Length == 0)
        {
            return "Imported Paper Card Recipe";
        }

        return char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }
}
