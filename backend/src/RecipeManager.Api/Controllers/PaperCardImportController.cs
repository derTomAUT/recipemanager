using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Infrastructure.Storage;
using RecipeManager.Api.Models;
using RecipeManager.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api/import/paper-card")]
[Authorize]
public class PaperCardImportController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly int[] AllowedServings = [2, 3, 4];

    private readonly AppDbContext _db;
    private readonly IStorageService _storage;
    private readonly IPaperCardVisionService _vision;
    private readonly ILogger<PaperCardImportController> _logger;

    public PaperCardImportController(
        AppDbContext db,
        IStorageService storage,
        IPaperCardVisionService vision,
        ILogger<PaperCardImportController> logger)
    {
        _db = db;
        _storage = storage;
        _vision = vision;
        _logger = logger;
    }

    [HttpPost("parse")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<PaperCardParseResponseDto>> Parse(
        [FromForm] IFormFile? frontImage,
        [FromForm] IFormFile? backImage,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (frontImage == null || backImage == null)
        {
            return BadRequest("Both frontImage and backImage are required.");
        }

        if (!IsSupportedImage(frontImage) || !IsSupportedImage(backImage))
        {
            return BadRequest("Only image/jpeg, image/png, and image/webp are supported.");
        }

        var householdMembership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(h => h.UserId == userId.Value && h.IsActive, cancellationToken);
        if (householdMembership == null)
        {
            return BadRequest("User does not belong to an active household.");
        }

        var household = await _db.Households.FindAsync([householdMembership.HouseholdId], cancellationToken);
        if (household == null)
        {
            return NotFound("Household not found.");
        }

        var vision = await _vision.ExtractAsync(frontImage, backImage, household, userId.Value, cancellationToken);
        var draftId = Guid.NewGuid();

        var (heroUrl, stepUrls) = await ExtractAndUploadImagesAsync(
            draftId,
            frontImage,
            backImage,
            vision.HeroImageRegion,
            vision.StepImageRegions,
            cancellationToken);
        var importedImages = new List<ImportedImageDto> { new(heroUrl, true, 0) };
        importedImages.AddRange(stepUrls.Select((url, index) => new ImportedImageDto(url, false, index + 1)));

        var draft = new PaperCardImportDraft
        {
            Id = draftId,
            HouseholdId = householdMembership.HouseholdId,
            CreatedByUserId = userId.Value,
            Title = vision.Title,
            Description = vision.Description,
            IngredientsByServingsJson = JsonSerializer.Serialize(vision.IngredientsByServings, JsonOptions),
            StepsJson = JsonSerializer.Serialize(vision.Steps, JsonOptions),
            HeroImageUrl = heroUrl,
            StepImageUrlsJson = JsonSerializer.Serialize(stepUrls, JsonOptions),
            WarningsJson = JsonSerializer.Serialize(vision.Warnings, JsonOptions),
            ConfidenceJson = JsonSerializer.Serialize(new { score = vision.ConfidenceScore }, JsonOptions),
            RawExtractedTextFront = vision.RawFrontText,
            RawExtractedTextBack = vision.RawBackText,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
            IsCommitted = false
        };

        _db.PaperCardImportDrafts.Add(draft);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new PaperCardParseResponseDto(
            draft.Id,
            draft.Title,
            draft.Description,
            vision.IngredientsByServings,
            vision.IngredientsByServings.Keys.Order().ToList(),
            vision.Steps,
            importedImages,
            vision.ConfidenceScore,
            vision.Warnings
        );

        return Ok(response);
    }

    [HttpPost("commit")]
    public async Task<ActionResult<CommitPaperCardImportResponse>> Commit(
        [FromBody] CommitPaperCardImportRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var draft = await _db.PaperCardImportDrafts.FirstOrDefaultAsync(d => d.Id == request.DraftId, cancellationToken);
        if (draft == null)
        {
            return NotFound("Draft not found.");
        }

        var membership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(h => h.UserId == userId.Value && h.IsActive, cancellationToken);
        if (membership == null || membership.HouseholdId != draft.HouseholdId)
        {
            return Forbid();
        }

        if (draft.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return BadRequest("Draft expired. Please parse images again.");
        }

        if (!AllowedServings.Contains(request.SelectedServings))
        {
            return BadRequest("SelectedServings must be one of: 2, 3, 4.");
        }

        var ingredientsByServings = JsonSerializer.Deserialize<Dictionary<int, List<IngredientDto>>>(draft.IngredientsByServingsJson, JsonOptions)
            ?? new Dictionary<int, List<IngredientDto>>();

        if (!ingredientsByServings.TryGetValue(request.SelectedServings, out var selectedIngredients))
        {
            return BadRequest("Selected serving scale is not available in draft.");
        }

        var draftSteps = JsonSerializer.Deserialize<List<StepDto>>(draft.StepsJson, JsonOptions) ?? new List<StepDto>();
        var stepImageUrls = JsonSerializer.Deserialize<List<string>>(draft.StepImageUrlsJson, JsonOptions) ?? new List<string>();

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = draft.HouseholdId,
            CreatedByUserId = userId.Value,
            Title = string.IsNullOrWhiteSpace(request.Title) ? draft.Title : request.Title.Trim(),
            Description = request.Description ?? draft.Description,
            Servings = request.SelectedServings,
            PrepMinutes = request.PrepMinutes,
            CookMinutes = request.CookMinutes,
            SourceUrl = "paper-card://hellofresh",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ingredientsToPersist = request.Ingredients?.Count > 0 ? request.Ingredients : selectedIngredients;
        for (var i = 0; i < ingredientsToPersist.Count; i++)
        {
            var ingredient = ingredientsToPersist[i];
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                OrderIndex = i,
                Name = ingredient.Name,
                Quantity = ingredient.Quantity,
                Unit = ingredient.Unit,
                Notes = ingredient.Notes
            });
        }

        var stepsToPersist = request.Steps?.Count > 0 ? request.Steps : draftSteps;
        for (var i = 0; i < stepsToPersist.Count; i++)
        {
            var step = stepsToPersist[i];
            recipe.Steps.Add(new RecipeStep
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                OrderIndex = i,
                Instruction = step.Instruction,
                TimerSeconds = step.TimerSeconds
            });
        }

        var tags = request.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            ?? new List<string>();
        if (tags.Count == 0) tags.Add("hellofresh");
        foreach (var tag in tags)
        {
            recipe.Tags.Add(new RecipeTag
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                Tag = tag
            });
        }

        recipe.Images.Add(new RecipeImage
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Url = draft.HeroImageUrl,
            IsTitleImage = true,
            OrderIndex = 0
        });

        for (var i = 0; i < stepImageUrls.Count; i++)
        {
            recipe.Images.Add(new RecipeImage
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                Url = stepImageUrls[i],
                IsTitleImage = false,
                OrderIndex = i + 1
            });
        }

        draft.IsCommitted = true;
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new CommitPaperCardImportResponse(recipe.Id));
    }

    private async Task<string> UploadTempImageAsync(Guid draftId, string side, IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = file.ContentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var safeFileName = $"temp_papercard_{draftId:N}_{side}{extension}";
        await using var stream = file.OpenReadStream();
        return await _storage.UploadAsync(stream, safeFileName, file.ContentType);
    }

    private async Task<(string HeroUrl, List<string> StepUrls)> ExtractAndUploadImagesAsync(
        Guid draftId,
        IFormFile frontImage,
        IFormFile backImage,
        ImageRegionDto? heroRegion,
        List<ImageRegionDto> stepRegions,
        CancellationToken cancellationToken)
    {
        try
        {
            using var front = await LoadImageAsync(frontImage, cancellationToken);
            using var back = await LoadImageAsync(backImage, cancellationToken);

            var heroUrl = heroRegion != null
                ? await UploadCroppedImageAsync(draftId, front, heroRegion, "hero", 0, cancellationToken)
                : string.Empty;
            if (string.IsNullOrWhiteSpace(heroUrl))
            {
                heroUrl = await UploadImageAsync(draftId, "front", front, cancellationToken);
            }

            var stepUrls = new List<string>();
            if (stepRegions.Count > 0)
            {
                for (var i = 0; i < stepRegions.Count; i++)
                {
                    var stepUrl = await UploadCroppedImageAsync(draftId, back, stepRegions[i], "step", i, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(stepUrl))
                    {
                        stepUrls.Add(stepUrl);
                    }
                }
            }

            if (stepUrls.Count == 0)
            {
                stepUrls.Add(await UploadImageAsync(draftId, "back", back, cancellationToken));
            }

            return (heroUrl, stepUrls);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crop paper card images, falling back to full uploads");
            var frontUrl = await UploadTempImageAsync(draftId, "front", frontImage, cancellationToken);
            var backUrl = await UploadTempImageAsync(draftId, "back", backImage, cancellationToken);
            return (frontUrl, new List<string> { backUrl });
        }
    }

    private static async Task<Image> LoadImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await Image.LoadAsync(stream, cancellationToken);
    }

    private async Task<string> UploadCroppedImageAsync(
        Guid draftId,
        Image sourceImage,
        ImageRegionDto region,
        string side,
        int index,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeRegion(region);
        if (normalized == null)
        {
            return string.Empty;
        }

        using var working = sourceImage.CloneAs<Rgba32>();
        if (normalized.RotationDegrees != 0)
        {
            working.Mutate(ctx => ctx.Rotate(normalized.RotationDegrees));
        }

        var rect = ToPixelRectangle(working.Width, working.Height, normalized);
        if (rect.Width <= 1 || rect.Height <= 1)
        {
            return string.Empty;
        }

        using var cropped = working.Clone(ctx => ctx.Crop(rect));
        return await UploadImageAsync(draftId, $"{side}_{index}", cropped, cancellationToken);
    }

    private async Task<string> UploadImageAsync(Guid draftId, string side, Image image, CancellationToken cancellationToken)
    {
        var safeFileName = $"temp_papercard_{draftId:N}_{side}.jpg";
        await using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, cancellationToken);
        ms.Position = 0;
        return await _storage.UploadAsync(ms, safeFileName, "image/jpeg");
    }

    private static ImageRegionDto? NormalizeRegion(ImageRegionDto region)
    {
        var x = Math.Clamp(region.X, 0.0, 1.0);
        var y = Math.Clamp(region.Y, 0.0, 1.0);
        var width = Math.Clamp(region.Width, 0.0, 1.0);
        var height = Math.Clamp(region.Height, 0.0, 1.0);
        if (x + width > 1.0) width = 1.0 - x;
        if (y + height > 1.0) height = 1.0 - y;
        if (width <= 0.01 || height <= 0.01)
        {
            return null;
        }

        var rotation = region.RotationDegrees switch
        {
            90 or 180 or 270 => region.RotationDegrees,
            _ => 0
        };

        return new ImageRegionDto(x, y, width, height, rotation);
    }

    private static Rectangle ToPixelRectangle(int imageWidth, int imageHeight, ImageRegionDto region)
    {
        var x = (int)Math.Round(region.X * imageWidth);
        var y = (int)Math.Round(region.Y * imageHeight);
        var width = (int)Math.Round(region.Width * imageWidth);
        var height = (int)Math.Round(region.Height * imageHeight);

        width = Math.Max(1, Math.Min(width, imageWidth - x));
        height = Math.Max(1, Math.Min(height, imageHeight - y));
        x = Math.Clamp(x, 0, imageWidth - 1);
        y = Math.Clamp(y, 0, imageHeight - 1);

        return new Rectangle(x, y, width, height);
    }

    private static bool IsSupportedImage(IFormFile file)
    {
        if (file.Length <= 0) return false;
        return file.ContentType is "image/jpeg" or "image/png" or "image/webp";
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return userId;
    }
}
