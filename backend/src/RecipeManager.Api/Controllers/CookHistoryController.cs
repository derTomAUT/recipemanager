using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class CookHistoryController : ControllerBase
{
    private readonly AppDbContext _db;

    public CookHistoryController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/recipes/{id}/cook - Mark recipe as cooked
    [HttpPost("recipes/{id:guid}/cook")]
    public async Task<ActionResult<CookEventDto>> MarkCooked(Guid id, [FromBody] MarkCookedRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, _) = membership.Value;

        var recipe = await _db.Recipes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId);
        if (recipe == null) return NotFound();

        var user = await _db.Users.FindAsync(userId.Value);

        var cookEvent = new CookEvent
        {
            Id = Guid.NewGuid(),
            RecipeId = id,
            UserId = userId.Value,
            HouseholdId = householdId,
            CookedAt = DateTime.UtcNow,
            Servings = request.Servings
        };

        _db.CookEvents.Add(cookEvent);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCookHistory), null, new CookEventDto(
            cookEvent.Id,
            recipe.Id,
            recipe.Title,
            recipe.Images.FirstOrDefault(i => i.IsTitleImage)?.Url ?? recipe.Images.FirstOrDefault()?.Url,
            user!.Id,
            user.Name,
            cookEvent.CookedAt,
            cookEvent.Servings
        ));
    }

    // GET /api/recipes/{id}/cook-history - Get cook history for a recipe
    [HttpGet("recipes/{id:guid}/cook-history")]
    public async Task<ActionResult<List<CookEventDto>>> GetRecipeCookHistory(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, _) = membership.Value;

        var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId);
        if (recipe == null) return NotFound();

        var history = await _db.CookEvents
            .Where(c => c.RecipeId == id)
            .Include(c => c.User)
            .Include(c => c.Recipe)
                .ThenInclude(r => r.Images)
            .OrderByDescending(c => c.CookedAt)
            .Select(c => new CookEventDto(
                c.Id,
                c.RecipeId,
                c.Recipe.Title,
                c.Recipe.Images.FirstOrDefault(i => i.IsTitleImage)!.Url ?? c.Recipe.Images.FirstOrDefault()!.Url,
                c.UserId,
                c.User.Name,
                c.CookedAt,
                c.Servings
            ))
            .ToListAsync();

        return Ok(history);
    }

    // GET /api/cook-history - Get household cook history feed
    [HttpGet("cook-history")]
    public async Task<ActionResult<PagedResult<CookEventDto>>> GetCookHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, _) = membership.Value;

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.CookEvents
            .Where(c => c.HouseholdId == householdId)
            .Include(c => c.User)
            .Include(c => c.Recipe)
                .ThenInclude(r => r.Images);

        var totalCount = await query.CountAsync();

        var history = await query
            .OrderByDescending(c => c.CookedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CookEventDto(
                c.Id,
                c.RecipeId,
                c.Recipe.Title,
                c.Recipe.Images.FirstOrDefault(i => i.IsTitleImage)!.Url ?? c.Recipe.Images.FirstOrDefault()!.Url,
                c.UserId,
                c.User.Name,
                c.CookedAt,
                c.Servings
            ))
            .ToListAsync();

        return Ok(new PagedResult<CookEventDto>(history, totalCount, page, pageSize));
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private async Task<(Guid householdId, string role)?> GetUserHouseholdAsync(Guid userId)
    {
        var member = await _db.HouseholdMembers.FirstOrDefaultAsync(hm => hm.UserId == userId);
        return member == null ? null : (member.HouseholdId, member.Role);
    }
}
