using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PreferencesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PreferencesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PreferencesDto>> GetPreferences()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var prefs = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId.Value);

        return Ok(new PreferencesDto(
            prefs?.Allergens ?? Array.Empty<string>(),
            prefs?.DislikedIngredients ?? Array.Empty<string>(),
            prefs?.FavoriteCuisines ?? Array.Empty<string>()
        ));
    }

    [HttpPut]
    public async Task<ActionResult<PreferencesDto>> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var prefs = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId.Value);
        if (prefs == null)
        {
            prefs = new UserPreference { Id = Guid.NewGuid(), UserId = userId.Value };
            _db.UserPreferences.Add(prefs);
        }

        prefs.Allergens = request.Allergens ?? Array.Empty<string>();
        prefs.DislikedIngredients = request.DislikedIngredients ?? Array.Empty<string>();
        prefs.FavoriteCuisines = request.FavoriteCuisines ?? Array.Empty<string>();

        await _db.SaveChangesAsync();

        return Ok(new PreferencesDto(prefs.Allergens, prefs.DislikedIngredients, prefs.FavoriteCuisines));
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
