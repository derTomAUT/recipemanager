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

        var vision = await _vision.ExtractAsync(frontImage, backImage, cancellationToken);
        var draftId = Guid.NewGuid();

        var frontUrl = await UploadTempImageAsync(draftId, "front", frontImage, cancellationToken);
        var backUrl = await UploadTempImageAsync(draftId, "back", backImage, cancellationToken);
        var importedImages = new List<ImportedImageDto>
        {
            new(frontUrl, true, 0),
            new(backUrl, false, 1)
        };

        var draft = new PaperCardImportDraft
        {
            Id = draftId,
            HouseholdId = householdMembership.HouseholdId,
            CreatedByUserId = userId.Value,
            Title = vision.Title,
            Description = vision.Description,
            IngredientsByServingsJson = JsonSerializer.Serialize(vision.IngredientsByServings, JsonOptions),
            StepsJson = JsonSerializer.Serialize(vision.Steps, JsonOptions),
            HeroImageUrl = frontUrl,
            StepImageUrlsJson = JsonSerializer.Serialize(new List<string> { backUrl }, JsonOptions),
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
