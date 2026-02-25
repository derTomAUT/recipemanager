using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.Services;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AiModelCatalogService _catalog;

    public AiController(AppDbContext db, AiModelCatalogService catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    [HttpGet("providers/models")]
    public async Task<ActionResult<List<string>>> GetModels([FromQuery] string provider)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var uid))
        {
            return Unauthorized();
        }

        var membership = await _db.HouseholdMembers.FirstOrDefaultAsync(h => h.UserId == uid);
        if (membership == null)
        {
            return BadRequest("User does not belong to a household");
        }

        if (membership.Role != "Owner")
        {
            return Forbid();
        }

        var household = await _db.Households.FindAsync(membership.HouseholdId);
        if (household == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(household.AiApiKeyEncrypted))
        {
            return BadRequest("API key not set");
        }

        var models = await _catalog.GetModelsAsync(provider, household.AiApiKeyEncrypted);
        return Ok(models);
    }
}
