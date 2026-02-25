using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Models;
using RecipeManager.Api.Services;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HouseholdController : ControllerBase
{
    private readonly AppDbContext _db;

    public HouseholdController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdDto>> CreateHousehold([FromBody] CreateHouseholdRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        // Check user doesn't already belong to a household
        var existingMembership = await _db.HouseholdMembers
            .AnyAsync(hm => hm.UserId == userId.Value);

        if (existingMembership)
        {
            return BadRequest("User already belongs to a household");
        }

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null)
        {
            return NotFound("User not found");
        }

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            InviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
        };

        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = userId.Value,
            Role = "Owner"
        };

        _db.Households.Add(household);
        _db.HouseholdMembers.Add(member);
        await _db.SaveChangesAsync();

        var dto = new HouseholdDto(
            household.Id,
            household.Name,
            household.InviteCode,
            new List<MemberDto>
            {
                new MemberDto(user.Id, user.Name, user.Email, member.Role)
            }
        );

        return CreatedAtAction(nameof(GetMyHousehold), dto);
    }

    [HttpPost("join")]
    public async Task<ActionResult<HouseholdDto>> JoinHousehold([FromBody] JoinHouseholdRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        // Check user doesn't already belong to a household
        var existingMembership = await _db.HouseholdMembers
            .AnyAsync(hm => hm.UserId == userId.Value);

        if (existingMembership)
        {
            return BadRequest("User already belongs to a household");
        }

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null)
        {
            return NotFound("User not found");
        }

        // Find household by InviteCode (case-insensitive)
        var household = await _db.Households
            .Include(h => h.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(h => h.InviteCode.ToUpper() == request.InviteCode.ToUpperInvariant());

        if (household == null)
        {
            return NotFound("Invalid invite code");
        }

        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = userId.Value,
            Role = "Member"
        };

        _db.HouseholdMembers.Add(member);
        await _db.SaveChangesAsync();

        var members = household.Members.Select(m =>
            new MemberDto(m.User.Id, m.User.Name, m.User.Email, m.Role)
        ).ToList();
        members.Add(new MemberDto(user.Id, user.Name, user.Email, member.Role));

        var dto = new HouseholdDto(
            household.Id,
            household.Name,
            household.InviteCode,
            members
        );

        return Ok(dto);
    }

    [HttpGet("me")]
    public async Task<ActionResult<HouseholdDto>> GetMyHousehold()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var membership = await _db.HouseholdMembers
            .Include(hm => hm.Household)
            .ThenInclude(h => h.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(hm => hm.UserId == userId.Value);

        if (membership == null)
        {
            return NotFound("User has no household");
        }

        var household = membership.Household;
        var members = household.Members.Select(m =>
            new MemberDto(m.User.Id, m.User.Name, m.User.Email, m.Role)
        ).ToList();

        var dto = new HouseholdDto(
            household.Id,
            household.Name,
            household.InviteCode,
            members
        );

        return Ok(dto);
    }

    [HttpGet("settings")]
    public async Task<ActionResult<HouseholdAiSettingsDto>> GetAiSettings()
    {
        var membership = await GetMembershipAsync();
        if (membership == null)
        {
            return Unauthorized();
        }

        var (householdId, role) = membership.Value;
        if (role != "Owner")
        {
            return Forbid();
        }

        var household = await _db.Households.FindAsync(householdId);
        if (household == null)
        {
            return NotFound();
        }

        return Ok(new HouseholdAiSettingsDto(
            household.AiProvider,
            household.AiModel,
            !string.IsNullOrEmpty(household.AiApiKeyEncrypted)
        ));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<HouseholdAiSettingsDto>> UpdateAiSettings(
        [FromBody] UpdateHouseholdAiSettingsRequest request,
        [FromServices] HouseholdAiSettingsService aiSettings)
    {
        var membership = await GetMembershipAsync();
        if (membership == null)
        {
            return Unauthorized();
        }

        var (householdId, role) = membership.Value;
        if (role != "Owner")
        {
            return Forbid();
        }

        var household = await _db.Households.FindAsync(householdId);
        if (household == null)
        {
            return NotFound();
        }

        household.AiProvider = request.AiProvider;
        household.AiModel = request.AiModel;

        var apiKey = request.ApiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            household.AiApiKeyEncrypted = aiSettings.Encrypt(apiKey);
        }

        await _db.SaveChangesAsync();

        return Ok(new HouseholdAiSettingsDto(
            household.AiProvider,
            household.AiModel,
            !string.IsNullOrEmpty(household.AiApiKeyEncrypted)
        ));
    }

    [HttpDelete("members/{targetUserId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid targetUserId)
    {
        var currentUserId = GetUserId();
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        // Get current user's membership
        var currentMembership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == currentUserId.Value);

        if (currentMembership == null)
        {
            return NotFound("Current user has no household");
        }

        // Verify current user is Owner
        if (currentMembership.Role != "Owner")
        {
            return Forbid();
        }

        // Cannot remove self if Owner
        if (targetUserId == currentUserId.Value)
        {
            return BadRequest("Owner cannot remove themselves from the household");
        }

        // Find the target member
        var targetMembership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == targetUserId && hm.HouseholdId == currentMembership.HouseholdId);

        if (targetMembership == null)
        {
            return NotFound("Member not found in household");
        }

        _db.HouseholdMembers.Remove(targetMembership);
        await _db.SaveChangesAsync();

        return NoContent();
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

    private async Task<(Guid householdId, string role)?> GetMembershipAsync()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return null;
        }

        var membership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == userId.Value);

        return membership == null ? null : (membership.HouseholdId, membership.Role);
    }
}
