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
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(5);
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
            InviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            InviteCodeCreatedAtUtc = DateTime.UtcNow,
            InviteCodeExpiresAtUtc = DateTime.UtcNow.Add(InviteLifetime)
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
        _db.HouseholdInvites.Add(new HouseholdInvite
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            InviteCode = household.InviteCode,
            CreatedAtUtc = household.InviteCodeCreatedAtUtc,
            ExpiresAtUtc = household.InviteCodeExpiresAtUtc,
            IsActive = true,
            CreatedByUserId = userId.Value
        });
        _db.HouseholdActivityLogs.Add(new HouseholdActivityLog
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            ActorUserId = userId.Value,
            EventType = "HouseholdCreated",
            Details = $"Invite {household.InviteCode} created"
        });
        await _db.SaveChangesAsync();

        var dto = new HouseholdDto(
            household.Id,
            household.Name,
            household.InviteCode,
            new List<MemberDto>
            {
                new MemberDto(user.Id, user.Name, user.Email, member.Role, member.IsActive)
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
        if (household.InviteCodeExpiresAtUtc <= DateTime.UtcNow)
        {
            return BadRequest("Invite code expired. Ask the owner to regenerate a new invite link.");
        }

        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = userId.Value,
            Role = "Member"
        };

        _db.HouseholdMembers.Add(member);
        _db.HouseholdActivityLogs.Add(new HouseholdActivityLog
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            ActorUserId = userId.Value,
            TargetUserId = userId.Value,
            EventType = "MemberJoined",
            Details = $"Joined with invite {household.InviteCode}"
        });
        await _db.SaveChangesAsync();

        var members = household.Members.Select(m =>
            new MemberDto(m.User.Id, m.User.Name, m.User.Email, m.Role, m.IsActive)
        ).ToList();
        members.Add(new MemberDto(user.Id, user.Name, user.Email, member.Role, member.IsActive));

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
            new MemberDto(m.User.Id, m.User.Name, m.User.Email, m.Role, m.IsActive)
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
            !string.IsNullOrEmpty(household.AiApiKeyEncrypted),
            household.Latitude,
            household.Longitude
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
        if (request.Latitude is < -90 or > 90)
        {
            return BadRequest("Latitude must be between -90 and 90.");
        }

        if (request.Longitude is < -180 or > 180)
        {
            return BadRequest("Longitude must be between -180 and 180.");
        }

        household.Latitude = request.Latitude;
        household.Longitude = request.Longitude;

        var apiKey = request.ApiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            household.AiApiKeyEncrypted = aiSettings.Encrypt(apiKey);
        }

        await _db.SaveChangesAsync();

        return Ok(new HouseholdAiSettingsDto(
            household.AiProvider,
            household.AiModel,
            !string.IsNullOrEmpty(household.AiApiKeyEncrypted),
            household.Latitude,
            household.Longitude
        ));
    }

    [HttpPut("settings/location")]
    public async Task<ActionResult<HouseholdAiSettingsDto>> UpdateLocation(
        [FromBody] UpdateHouseholdLocationRequest request)
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

        if (request.Latitude is < -90 or > 90)
        {
            return BadRequest("Latitude must be between -90 and 90.");
        }

        if (request.Longitude is < -180 or > 180)
        {
            return BadRequest("Longitude must be between -180 and 180.");
        }

        household.Latitude = request.Latitude;
        household.Longitude = request.Longitude;
        await _db.SaveChangesAsync();

        return Ok(new HouseholdAiSettingsDto(
            household.AiProvider,
            household.AiModel,
            !string.IsNullOrEmpty(household.AiApiKeyEncrypted),
            household.Latitude,
            household.Longitude
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
            .FirstOrDefaultAsync(hm => hm.UserId == currentUserId.Value && hm.IsActive);

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
        _db.HouseholdActivityLogs.Add(new HouseholdActivityLog
        {
            Id = Guid.NewGuid(),
            HouseholdId = currentMembership.HouseholdId,
            ActorUserId = currentUserId.Value,
            TargetUserId = targetUserId,
            EventType = "MemberRemoved"
        });
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("members/{targetUserId:guid}/disable")]
    public async Task<IActionResult> DisableMember(Guid targetUserId)
    {
        var currentUserId = GetUserId();
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var currentMembership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == currentUserId.Value && hm.IsActive);

        if (currentMembership == null)
        {
            return NotFound("Current user has no active household");
        }

        if (currentMembership.Role != "Owner")
        {
            return Forbid();
        }

        var targetMembership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == targetUserId && hm.HouseholdId == currentMembership.HouseholdId);

        if (targetMembership == null)
        {
            return NotFound("Member not found in household");
        }

        if (targetMembership.IsActive)
        {
            var activeCount = await _db.HouseholdMembers
                .CountAsync(hm => hm.HouseholdId == currentMembership.HouseholdId && hm.IsActive);

            if (activeCount <= 1)
            {
                return BadRequest("At least one active member must remain in the household");
            }

            if (targetMembership.Role == "Owner")
            {
                var activeOwnerCount = await _db.HouseholdMembers
                    .CountAsync(hm =>
                        hm.HouseholdId == currentMembership.HouseholdId &&
                        hm.IsActive &&
                        hm.Role == "Owner");

                if (activeOwnerCount <= 1)
                {
                    return BadRequest("Cannot disable the last active owner. Promote another member to Owner first.");
                }
            }

            if (targetUserId == currentUserId.Value)
            {
                return BadRequest("Owner cannot disable themselves");
            }
        }

        targetMembership.IsActive = false;
        _db.HouseholdActivityLogs.Add(new HouseholdActivityLog
        {
            Id = Guid.NewGuid(),
            HouseholdId = currentMembership.HouseholdId,
            ActorUserId = currentUserId.Value,
            TargetUserId = targetUserId,
            EventType = "MemberDisabled"
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("members/{targetUserId:guid}/enable")]
    public async Task<IActionResult> EnableMember(Guid targetUserId)
    {
        var currentUserId = GetUserId();
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var currentMembership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == currentUserId.Value && hm.IsActive);

        if (currentMembership == null)
        {
            return NotFound("Current user has no active household");
        }

        if (currentMembership.Role != "Owner")
        {
            return Forbid();
        }

        var targetMembership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == targetUserId && hm.HouseholdId == currentMembership.HouseholdId);

        if (targetMembership == null)
        {
            return NotFound("Member not found in household");
        }

        targetMembership.IsActive = true;
        _db.HouseholdActivityLogs.Add(new HouseholdActivityLog
        {
            Id = Guid.NewGuid(),
            HouseholdId = currentMembership.HouseholdId,
            ActorUserId = currentUserId.Value,
            TargetUserId = targetUserId,
            EventType = "MemberEnabled"
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("members/{targetUserId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid targetUserId, [FromBody] UpdateHouseholdMemberRoleRequest request)
    {
        var currentUserId = GetUserId();
        if (currentUserId == null) return Unauthorized();

        var currentMembership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == currentUserId.Value && hm.IsActive);
        if (currentMembership == null) return NotFound("Current user has no active household");
        if (currentMembership.Role != "Owner") return Forbid();

        var role = request.Role.Trim();
        if (role != "Owner" && role != "Member" && role != "Viewer")
        {
            return BadRequest("Role must be Owner, Member, or Viewer");
        }

        var targetMembership = await _db.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == targetUserId && hm.HouseholdId == currentMembership.HouseholdId);
        if (targetMembership == null) return NotFound("Member not found in household");

        if (targetMembership.Role == "Owner" && role != "Owner")
        {
            var activeOwnerCount = await _db.HouseholdMembers
                .CountAsync(hm => hm.HouseholdId == currentMembership.HouseholdId && hm.IsActive && hm.Role == "Owner");
            if (activeOwnerCount <= 1)
            {
                return BadRequest("At least one active owner must remain in the household");
            }
        }

        targetMembership.Role = role;
        _db.HouseholdActivityLogs.Add(new HouseholdActivityLog
        {
            Id = Guid.NewGuid(),
            HouseholdId = currentMembership.HouseholdId,
            ActorUserId = currentUserId.Value,
            TargetUserId = targetUserId,
            EventType = "MemberRoleUpdated",
            Details = $"Role set to {role}"
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("invite")]
    public async Task<ActionResult<HouseholdInviteDto>> GetInvite()
    {
        var membership = await GetMembershipAsync();
        if (membership == null) return Unauthorized();
        if (membership.Value.role != "Owner") return Forbid();

        var household = await _db.Households.FindAsync(membership.Value.householdId);
        if (household == null) return NotFound();

        return Ok(new HouseholdInviteDto(
            household.InviteCode,
            household.InviteCodeCreatedAtUtc,
            household.InviteCodeExpiresAtUtc,
            household.InviteCodeExpiresAtUtc <= DateTime.UtcNow
        ));
    }

    [HttpPost("invite/regenerate")]
    public async Task<ActionResult<HouseholdInviteDto>> RegenerateInvite()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetMembershipAsync();
        if (membership == null) return Unauthorized();
        if (membership.Value.role != "Owner") return Forbid();

        var household = await _db.Households.FindAsync(membership.Value.householdId);
        if (household == null) return NotFound();

        var now = DateTime.UtcNow;
        household.InviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        household.InviteCodeCreatedAtUtc = now;
        household.InviteCodeExpiresAtUtc = now.Add(InviteLifetime);

        var activeInvites = await _db.HouseholdInvites
            .Where(i => i.HouseholdId == household.Id && i.IsActive)
            .ToListAsync();
        foreach (var invite in activeInvites)
        {
            invite.IsActive = false;
        }

        _db.HouseholdInvites.Add(new HouseholdInvite
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            InviteCode = household.InviteCode,
            CreatedAtUtc = household.InviteCodeCreatedAtUtc,
            ExpiresAtUtc = household.InviteCodeExpiresAtUtc,
            IsActive = true,
            CreatedByUserId = userId.Value
        });
        _db.HouseholdActivityLogs.Add(new HouseholdActivityLog
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            ActorUserId = userId.Value,
            EventType = "InviteRegenerated",
            Details = $"New invite {household.InviteCode}"
        });

        await _db.SaveChangesAsync();
        return Ok(new HouseholdInviteDto(
            household.InviteCode,
            household.InviteCodeCreatedAtUtc,
            household.InviteCodeExpiresAtUtc,
            false
        ));
    }

    [HttpGet("activity")]
    public async Task<ActionResult<List<HouseholdActivityDto>>> GetActivity([FromQuery] int limit = 50)
    {
        var membership = await GetMembershipAsync();
        if (membership == null) return Unauthorized();
        if (membership.Value.role != "Owner") return Forbid();

        limit = Math.Clamp(limit, 1, 200);
        var items = await _db.HouseholdActivityLogs
            .Where(l => l.HouseholdId == membership.Value.householdId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(limit)
            .Select(l => new HouseholdActivityDto(
                l.Id,
                l.EventType,
                l.ActorUserId,
                l.TargetUserId,
                l.Details,
                l.CreatedAtUtc
            ))
            .ToListAsync();

        return Ok(items);
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
            .FirstOrDefaultAsync(hm => hm.UserId == userId.Value && hm.IsActive);

        return membership == null ? null : (membership.HouseholdId, membership.Role);
    }
}
