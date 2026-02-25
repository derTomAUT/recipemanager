using System.Security.Claims;
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
[Route("api/[controller]")]
[Authorize]
public class RecipeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storageService;
    private readonly RecommendationService _recommendationService;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    public RecipeController(AppDbContext db, IStorageService storageService, RecommendationService recommendationService)
    {
        _db = db;
        _storageService = storageService;
        _recommendationService = recommendationService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<RecipeListItemDto>>> GetRecipes(
        [FromQuery] string? search,
        [FromQuery] string? tags,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, _) = membership.Value;

        // Clamp pageSize to reasonable bounds
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Build the base query
        var query = _db.Recipes
            .Where(r => r.HouseholdId == householdId)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(r =>
                r.Title.ToLower().Contains(searchLower) ||
                (r.Description != null && r.Description.ToLower().Contains(searchLower)));
        }

        // Apply tags filter
        if (!string.IsNullOrWhiteSpace(tags))
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLower())
                .ToList();

            if (tagList.Count > 0)
            {
                query = query.Where(r => r.Tags.Any(rt => tagList.Contains(rt.Tag.ToLower())));
            }
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();

        // Apply pagination and load data
        var recipes = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(r => r.Images)
            .Include(r => r.Tags)
            .ToListAsync();

        // Get cook stats for these recipes
        var recipeIds = recipes.Select(r => r.Id).ToList();
        var cookStats = await _db.CookEvents
            .Where(ce => recipeIds.Contains(ce.RecipeId))
            .GroupBy(ce => ce.RecipeId)
            .Select(g => new
            {
                RecipeId = g.Key,
                CookCount = g.Count(),
                LastCooked = g.Max(ce => ce.CookedAt)
            })
            .ToListAsync();

        var cookStatsDict = cookStats.ToDictionary(cs => cs.RecipeId);

        var items = recipes.Select(r =>
        {
            var stats = cookStatsDict.GetValueOrDefault(r.Id);
            var titleImage = r.Images.FirstOrDefault(i => i.IsTitleImage) ?? r.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();

            return new RecipeListItemDto(
                r.Id,
                r.Title,
                r.Description,
                r.Servings,
                r.PrepMinutes,
                r.CookMinutes,
                titleImage?.Url,
                r.Tags.Select(t => t.Tag).ToList(),
                stats?.CookCount ?? 0,
                stats?.LastCooked,
                r.CreatedAt
            );
        }).ToList();

        return Ok(new PagedResult<RecipeListItemDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("recommended")]
    public async Task<ActionResult<List<RecipeListItemDto>>> GetRecommendedRecipes([FromQuery] int count = 10)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, _) = membership.Value;

        count = Math.Clamp(count, 1, 20);

        var recipes = await _recommendationService.GetRecommendedRecipesAsync(userId.Value, householdId, count);

        // Get cook stats
        var recipeIds = recipes.Select(r => r.Id).ToList();
        var cookStats = await _db.CookEvents
            .Where(c => recipeIds.Contains(c.RecipeId))
            .GroupBy(c => c.RecipeId)
            .Select(g => new { RecipeId = g.Key, Count = g.Count(), LastCooked = g.Max(c => c.CookedAt) })
            .ToDictionaryAsync(x => x.RecipeId, x => (x.Count, x.LastCooked));

        var result = recipes.Select(r =>
        {
            var titleImage = r.Images.FirstOrDefault(i => i.IsTitleImage) ?? r.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();
            cookStats.TryGetValue(r.Id, out var stats);

            return new RecipeListItemDto(
                r.Id,
                r.Title,
                r.Description,
                r.Servings,
                r.PrepMinutes,
                r.CookMinutes,
                titleImage?.Url,
                r.Tags.Select(t => t.Tag).ToList(),
                stats.Count,
                stats.LastCooked == default ? null : stats.LastCooked,
                r.CreatedAt
            );
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDetailDto>> GetRecipe(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, _) = membership.Value;

        var recipe = await _db.Recipes
            .Where(r => r.Id == id && r.HouseholdId == householdId)
            .Include(r => r.Ingredients.OrderBy(i => i.OrderIndex))
            .Include(r => r.Steps.OrderBy(s => s.OrderIndex))
            .Include(r => r.Images.OrderBy(i => i.OrderIndex))
            .Include(r => r.Tags)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound();
        }

        // Get cook stats
        var cookStats = await _db.CookEvents
            .Where(ce => ce.RecipeId == id)
            .GroupBy(ce => ce.RecipeId)
            .Select(g => new
            {
                CookCount = g.Count(),
                LastCooked = g.Max(ce => ce.CookedAt)
            })
            .FirstOrDefaultAsync();

        var dto = new RecipeDetailDto(
            recipe.Id,
            recipe.Title,
            recipe.Description,
            recipe.Servings,
            recipe.PrepMinutes,
            recipe.CookMinutes,
            recipe.Ingredients.Select(i => new RecipeIngredientDto(
                i.Id, i.OrderIndex, i.Name, i.Quantity, i.Unit, i.Notes
            )).ToList(),
            recipe.Steps.Select(s => new RecipeStepDto(
                s.Id, s.OrderIndex, s.Instruction, s.TimerSeconds
            )).ToList(),
            recipe.Images.Select(i => new RecipeImageDto(
                i.Id, i.Url, i.IsTitleImage, i.OrderIndex
            )).ToList(),
            recipe.Tags.Select(t => t.Tag).ToList(),
            cookStats?.CookCount ?? 0,
            cookStats?.LastCooked,
            recipe.CreatedAt,
            recipe.UpdatedAt,
            recipe.CreatedByUserId
        );

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDetailDto>> CreateRecipe([FromBody] CreateRecipeRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, _) = membership.Value;

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            Title = request.Title,
            Description = request.Description,
            Servings = request.Servings,
            PrepMinutes = request.PrepMinutes,
            CookMinutes = request.CookMinutes,
            CreatedByUserId = userId.Value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add ingredients
        if (request.Ingredients != null)
        {
            for (int i = 0; i < request.Ingredients.Count; i++)
            {
                var ing = request.Ingredients[i];
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    OrderIndex = i,
                    Name = ing.Name,
                    Quantity = ing.Quantity,
                    Unit = ing.Unit,
                    Notes = ing.Notes
                });
            }
        }

        // Add steps
        if (request.Steps != null)
        {
            for (int i = 0; i < request.Steps.Count; i++)
            {
                var step = request.Steps[i];
                recipe.Steps.Add(new RecipeStep
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    OrderIndex = i,
                    Instruction = step.Instruction,
                    TimerSeconds = step.TimerSeconds
                });
            }
        }

        // Add tags
        if (request.Tags != null)
        {
            foreach (var tag in request.Tags.Distinct())
            {
                recipe.Tags.Add(new RecipeTag
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Tag = tag
                });
            }
        }

        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        var dto = new RecipeDetailDto(
            recipe.Id,
            recipe.Title,
            recipe.Description,
            recipe.Servings,
            recipe.PrepMinutes,
            recipe.CookMinutes,
            recipe.Ingredients.OrderBy(i => i.OrderIndex).Select(i => new RecipeIngredientDto(
                i.Id, i.OrderIndex, i.Name, i.Quantity, i.Unit, i.Notes
            )).ToList(),
            recipe.Steps.OrderBy(s => s.OrderIndex).Select(s => new RecipeStepDto(
                s.Id, s.OrderIndex, s.Instruction, s.TimerSeconds
            )).ToList(),
            new List<RecipeImageDto>(), // No images on creation
            recipe.Tags.Select(t => t.Tag).ToList(),
            0, // CookCount
            null, // LastCooked
            recipe.CreatedAt,
            recipe.UpdatedAt,
            recipe.CreatedByUserId
        );

        return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDetailDto>> UpdateRecipe(Guid id, [FromBody] UpdateRecipeRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, _) = membership.Value;

        var recipe = await _db.Recipes
            .Where(r => r.Id == id && r.HouseholdId == householdId)
            .Include(r => r.Ingredients)
            .Include(r => r.Steps)
            .Include(r => r.Images)
            .Include(r => r.Tags)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound();
        }

        // Update basic properties
        recipe.Title = request.Title;
        recipe.Description = request.Description;
        recipe.Servings = request.Servings;
        recipe.PrepMinutes = request.PrepMinutes;
        recipe.CookMinutes = request.CookMinutes;
        recipe.UpdatedAt = DateTime.UtcNow;

        // Replace ingredients
        _db.RecipeIngredients.RemoveRange(recipe.Ingredients);
        recipe.Ingredients.Clear();

        if (request.Ingredients != null)
        {
            for (int i = 0; i < request.Ingredients.Count; i++)
            {
                var ing = request.Ingredients[i];
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    OrderIndex = i,
                    Name = ing.Name,
                    Quantity = ing.Quantity,
                    Unit = ing.Unit,
                    Notes = ing.Notes
                });
            }
        }

        // Replace steps
        _db.RecipeSteps.RemoveRange(recipe.Steps);
        recipe.Steps.Clear();

        if (request.Steps != null)
        {
            for (int i = 0; i < request.Steps.Count; i++)
            {
                var step = request.Steps[i];
                recipe.Steps.Add(new RecipeStep
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    OrderIndex = i,
                    Instruction = step.Instruction,
                    TimerSeconds = step.TimerSeconds
                });
            }
        }

        // Replace tags (keep images as-is)
        _db.RecipeTags.RemoveRange(recipe.Tags);
        recipe.Tags.Clear();

        if (request.Tags != null)
        {
            foreach (var tag in request.Tags.Distinct())
            {
                recipe.Tags.Add(new RecipeTag
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Tag = tag
                });
            }
        }

        await _db.SaveChangesAsync();

        // Get cook stats
        var cookStats = await _db.CookEvents
            .Where(ce => ce.RecipeId == id)
            .GroupBy(ce => ce.RecipeId)
            .Select(g => new
            {
                CookCount = g.Count(),
                LastCooked = g.Max(ce => ce.CookedAt)
            })
            .FirstOrDefaultAsync();

        var dto = new RecipeDetailDto(
            recipe.Id,
            recipe.Title,
            recipe.Description,
            recipe.Servings,
            recipe.PrepMinutes,
            recipe.CookMinutes,
            recipe.Ingredients.OrderBy(i => i.OrderIndex).Select(i => new RecipeIngredientDto(
                i.Id, i.OrderIndex, i.Name, i.Quantity, i.Unit, i.Notes
            )).ToList(),
            recipe.Steps.OrderBy(s => s.OrderIndex).Select(s => new RecipeStepDto(
                s.Id, s.OrderIndex, s.Instruction, s.TimerSeconds
            )).ToList(),
            recipe.Images.OrderBy(i => i.OrderIndex).Select(i => new RecipeImageDto(
                i.Id, i.Url, i.IsTitleImage, i.OrderIndex
            )).ToList(),
            recipe.Tags.Select(t => t.Tag).ToList(),
            cookStats?.CookCount ?? 0,
            cookStats?.LastCooked,
            recipe.CreatedAt,
            recipe.UpdatedAt,
            recipe.CreatedByUserId
        );

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRecipe(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, role) = membership.Value;

        var recipe = await _db.Recipes
            .Where(r => r.Id == id && r.HouseholdId == householdId)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound();
        }

        // Authorization: Owner can delete any recipe, Member can only delete their own
        if (role != "Owner" && recipe.CreatedByUserId != userId.Value)
        {
            return Forbid();
        }

        _db.Recipes.Remove(recipe);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/images")]
    public async Task<ActionResult<RecipeImageDto>> UploadImage(Guid id, IFormFile file)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, _) = membership.Value;

        // Verify recipe exists and belongs to household
        var recipe = await _db.Recipes
            .Where(r => r.Id == id && r.HouseholdId == householdId)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound();
        }

        // Validate file
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest("File size exceeds 10MB limit");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest("Invalid file type. Allowed: jpg, jpeg, png, gif, webp");
        }

        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (string.IsNullOrEmpty(file.ContentType) || !allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return BadRequest("Invalid content type. Allowed: image/jpeg, image/png, image/gif, image/webp");
        }

        // Upload to storage
        string url;
        try
        {
            using var stream = file.OpenReadStream();
            url = await _storageService.UploadAsync(stream, file.FileName, file.ContentType);
        }
        catch (Exception)
        {
            return StatusCode(500, "Failed to upload image");
        }

        // Create RecipeImage record
        var existingCount = await _db.RecipeImages.CountAsync(i => i.RecipeId == id);
        var image = new RecipeImage
        {
            Id = Guid.NewGuid(),
            RecipeId = id,
            Url = url,
            IsTitleImage = existingCount == 0, // First image is title
            OrderIndex = existingCount
        };

        _db.RecipeImages.Add(image);
        await _db.SaveChangesAsync();

        var dto = new RecipeImageDto(image.Id, image.Url, image.IsTitleImage, image.OrderIndex);
        return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, dto);
    }

    [HttpPatch("{id:guid}/title-image")]
    public async Task<ActionResult<List<RecipeImageDto>>> SetTitleImage(Guid id, [FromBody] SetTitleImageRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, _) = membership.Value;

        // Verify recipe exists and belongs to household
        var recipe = await _db.Recipes
            .Where(r => r.Id == id && r.HouseholdId == householdId)
            .Include(r => r.Images)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound();
        }

        // Verify the image exists and belongs to this recipe
        var targetImage = recipe.Images.FirstOrDefault(i => i.Id == request.ImageId);
        if (targetImage == null)
        {
            return NotFound("Image not found");
        }

        // Clear IsTitleImage on all images and set on the specified one
        foreach (var img in recipe.Images)
        {
            img.IsTitleImage = img.Id == request.ImageId;
        }

        await _db.SaveChangesAsync();

        var dtos = recipe.Images
            .OrderBy(i => i.OrderIndex)
            .Select(i => new RecipeImageDto(i.Id, i.Url, i.IsTitleImage, i.OrderIndex))
            .ToList();

        return Ok(dtos);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, _) = membership.Value;

        // Verify recipe exists and belongs to household
        var recipe = await _db.Recipes
            .Where(r => r.Id == id && r.HouseholdId == householdId)
            .Include(r => r.Images)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound();
        }

        // Find the image to delete
        var image = recipe.Images.FirstOrDefault(i => i.Id == imageId);
        if (image == null)
        {
            return NotFound("Image not found");
        }

        // Delete from storage
        try
        {
            await _storageService.DeleteAsync(image.Url);
        }
        catch (Exception)
        {
            // Log would go here; continue with DB deletion
        }

        // Remove from database
        var wasTitleImage = image.IsTitleImage;
        _db.RecipeImages.Remove(image);
        await _db.SaveChangesAsync();

        // If deleted image was title image and others exist, set first remaining as title
        if (wasTitleImage)
        {
            var remainingImages = await _db.RecipeImages
                .Where(i => i.RecipeId == id)
                .OrderBy(i => i.OrderIndex)
                .ToListAsync();

            if (remainingImages.Count > 0)
            {
                remainingImages[0].IsTitleImage = true;
                await _db.SaveChangesAsync();
            }
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/favorite")]
    public async Task<IActionResult> AddFavorite(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, _) = membership.Value;

        // Verify recipe exists and belongs to household
        var recipeExists = await _db.Recipes
            .AnyAsync(r => r.Id == id && r.HouseholdId == householdId);

        if (!recipeExists)
        {
            return NotFound();
        }

        // Check if already favorited
        var existing = await _db.FavoriteRecipes
            .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.RecipeId == id);

        if (existing != null)
        {
            return Ok(); // Already favorited
        }

        var favorite = new FavoriteRecipe
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            RecipeId = id,
            FavoritedAt = DateTime.UtcNow
        };

        _db.FavoriteRecipes.Add(favorite);
        await _db.SaveChangesAsync();

        return Created($"/api/recipes/{id}/favorite", null);
    }

    [HttpDelete("{id:guid}/favorite")]
    public async Task<IActionResult> RemoveFavorite(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var favorite = await _db.FavoriteRecipes
            .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.RecipeId == id);

        if (favorite == null)
        {
            return NoContent(); // Not favorited, but that's fine
        }

        _db.FavoriteRecipes.Remove(favorite);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("favorites")]
    public async Task<ActionResult<PagedResult<RecipeListItemDto>>> GetFavorites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        var (householdId, _) = membership.Value;

        // Clamp pageSize to reasonable bounds
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Get total count of favorites (filtered by household)
        var totalCount = await _db.FavoriteRecipes
            .Where(f => f.UserId == userId.Value && f.Recipe.HouseholdId == householdId)
            .CountAsync();

        // Get favorites with recipe details (filtered by household)
        var favorites = await _db.FavoriteRecipes
            .Where(f => f.UserId == userId.Value && f.Recipe.HouseholdId == householdId)
            .OrderByDescending(f => f.FavoritedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(f => f.Recipe)
                .ThenInclude(r => r.Tags)
            .Include(f => f.Recipe)
                .ThenInclude(r => r.Images)
            .Select(f => f.Recipe)
            .ToListAsync();

        // Get cook stats for these recipes
        var recipeIds = favorites.Select(r => r.Id).ToList();
        var cookStats = await _db.CookEvents
            .Where(ce => recipeIds.Contains(ce.RecipeId))
            .GroupBy(ce => ce.RecipeId)
            .Select(g => new
            {
                RecipeId = g.Key,
                CookCount = g.Count(),
                LastCooked = g.Max(ce => ce.CookedAt)
            })
            .ToListAsync();

        var cookStatsDict = cookStats.ToDictionary(cs => cs.RecipeId);

        var items = favorites.Select(r =>
        {
            var stats = cookStatsDict.GetValueOrDefault(r.Id);
            var titleImage = r.Images.FirstOrDefault(i => i.IsTitleImage) ?? r.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();

            return new RecipeListItemDto(
                r.Id,
                r.Title,
                r.Description,
                r.Servings,
                r.PrepMinutes,
                r.CookMinutes,
                titleImage?.Url,
                r.Tags.Select(t => t.Tag).ToList(),
                stats?.CookCount ?? 0,
                stats?.LastCooked,
                r.CreatedAt
            );
        }).ToList();

        return Ok(new PagedResult<RecipeListItemDto>(items, totalCount, page, pageSize));
    }

    private async Task<(Guid householdId, string role)?> GetUserHouseholdAsync(Guid userId)
    {
        var member = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == userId);
        return member == null ? null : (member.HouseholdId, member.Role);
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
