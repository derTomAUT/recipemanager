using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api/debug")]
[Authorize]
public class DebugController : ControllerBase
{
    private readonly AppDbContext _db;

    public DebugController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("ai")]
    public async Task<ActionResult<PagedResult<AiDebugLogDto>>> GetAiLogs(
        [FromQuery] string? provider,
        [FromQuery] string? operation,
        [FromQuery] bool? success,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var membership = await _db.HouseholdMembers.FirstOrDefaultAsync(h => h.UserId == userId);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AiDebugLogs
            .Where(x => x.HouseholdId == membership.HouseholdId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(x => x.Provider == provider);
        }

        if (!string.IsNullOrWhiteSpace(operation))
        {
            query = query.Where(x => x.Operation == operation);
        }

        if (success.HasValue)
        {
            query = query.Where(x => x.Success == success.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AiDebugLogDto(
                x.Id,
                x.CreatedAtUtc,
                x.Provider,
                x.Model,
                x.Operation,
                x.RequestJsonSanitized,
                x.ResponseJsonSanitized,
                x.StatusCode,
                x.Success,
                x.Error))
            .ToListAsync();

        return Ok(new PagedResult<AiDebugLogDto>(items, totalCount, page, pageSize));
    }
}
