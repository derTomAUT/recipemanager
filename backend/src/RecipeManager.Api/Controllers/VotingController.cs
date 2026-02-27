using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api/voting")]
[Authorize]
public class VotingController : ControllerBase
{
    private readonly AppDbContext _db;
    private const int MaxNominations = 4;

    public VotingController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/voting/rounds - Create a new voting round (Owner only)
    [HttpPost("rounds")]
    public async Task<ActionResult<VotingRoundDto>> CreateRound()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, role) = membership.Value;

        if (role != "Owner")
        {
            return Forbid();
        }

        // Check for existing active round
        var existingActive = await _db.VotingRounds
            .AnyAsync(r => r.HouseholdId == householdId && r.ClosedAt == null);

        if (existingActive)
        {
            return Conflict("An active voting round already exists");
        }

        var round = new VotingRound
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            CreatedAt = DateTime.UtcNow
        };

        _db.VotingRounds.Add(round);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetActiveRound), null, new VotingRoundDto(
            round.Id,
            round.CreatedAt,
            null,
            null,
            null,
            new List<NominationDto>(),
            0,
            false
        ));
    }

    // GET /api/voting/rounds/active - Get the active voting round
    [HttpGet("rounds/active")]
    public async Task<ActionResult<VotingRoundDto?>> GetActiveRound()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, _) = membership.Value;

        var round = await _db.VotingRounds
            .Where(r => r.HouseholdId == householdId && r.ClosedAt == null)
            .Include(r => r.Nominations)
                .ThenInclude(n => n.Recipe)
                    .ThenInclude(r => r.Images)
            .Include(r => r.Votes)
            .FirstOrDefaultAsync();

        if (round == null)
        {
            return Ok(null);
        }

        // Get user info for nominations
        var nominatorIds = round.Nominations.Select(n => n.NominatedByUserId).Distinct().ToList();
        var users = await _db.Users
            .Where(u => nominatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var nominations = round.Nominations.Select(n =>
        {
            var titleImage = n.Recipe.Images.FirstOrDefault(i => i.IsTitleImage) ?? n.Recipe.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();
            var voteCount = round.Votes.Count(v => v.RecipeId == n.RecipeId);
            return new NominationDto(
                n.RecipeId,
                n.Recipe.Title,
                titleImage?.Url,
                n.NominatedByUserId,
                users.GetValueOrDefault(n.NominatedByUserId, "Unknown"),
                voteCount
            );
        }).ToList();

        var userHasVoted = round.Votes.Any(v => v.UserId == userId.Value);

        return Ok(new VotingRoundDto(
            round.Id,
            round.CreatedAt,
            null,
            null,
            null,
            nominations,
            round.Votes.Count,
            userHasVoted
        ));
    }

    // POST /api/voting/rounds/{id}/nominations - Nominate a recipe
    [HttpPost("rounds/{id:guid}/nominations")]
    public async Task<ActionResult<NominationDto>> Nominate(Guid id, [FromBody] NominateRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, _) = membership.Value;

        var round = await _db.VotingRounds
            .Include(r => r.Nominations)
            .FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId && r.ClosedAt == null);

        if (round == null)
        {
            return NotFound("Active voting round not found");
        }

        // Check max nominations limit (4 total in the round)
        if (round.Nominations.Count >= MaxNominations)
        {
            return Conflict("Maximum number of nominations reached");
        }

        // Check if recipe already nominated
        if (round.Nominations.Any(n => n.RecipeId == request.RecipeId))
        {
            return Conflict("Recipe already nominated");
        }

        // Verify recipe exists and belongs to household
        var recipe = await _db.Recipes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId && r.HouseholdId == householdId);

        if (recipe == null)
        {
            return NotFound("Recipe not found");
        }

        var user = await _db.Users.FindAsync(userId.Value);

        var nomination = new VotingNomination
        {
            Id = Guid.NewGuid(),
            RoundId = id,
            RecipeId = request.RecipeId,
            NominatedByUserId = userId.Value,
            NominatedAt = DateTime.UtcNow
        };

        _db.VotingNominations.Add(nomination);
        await _db.SaveChangesAsync();

        var titleImage = recipe.Images.FirstOrDefault(i => i.IsTitleImage) ?? recipe.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();

        return Created($"/api/voting/rounds/{id}/nominations/{recipe.Id}", new NominationDto(
            recipe.Id,
            recipe.Title,
            titleImage?.Url,
            userId.Value,
            user!.Name,
            0
        ));
    }

    // DELETE /api/voting/rounds/{id}/nominations/{recipeId} - Withdraw own nomination
    [HttpDelete("rounds/{id:guid}/nominations/{recipeId:guid}")]
    public async Task<IActionResult> WithdrawNomination(Guid id, Guid recipeId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, _) = membership.Value;

        var round = await _db.VotingRounds
            .FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId && r.ClosedAt == null);

        if (round == null)
        {
            return NotFound("Active voting round not found");
        }

        var nomination = await _db.VotingNominations
            .FirstOrDefaultAsync(n => n.RoundId == id && n.RecipeId == recipeId && n.NominatedByUserId == userId.Value);

        if (nomination == null)
        {
            return NotFound("Nomination not found or not owned by user");
        }

        // Also remove any votes for this recipe in this round
        var votesToRemove = await _db.VotingVotes
            .Where(v => v.RoundId == id && v.RecipeId == recipeId)
            .ToListAsync();

        _db.VotingVotes.RemoveRange(votesToRemove);
        _db.VotingNominations.Remove(nomination);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // POST /api/voting/rounds/{id}/votes - Cast a vote
    [HttpPost("rounds/{id:guid}/votes")]
    public async Task<IActionResult> Vote(Guid id, [FromBody] VoteRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, _) = membership.Value;

        var round = await _db.VotingRounds
            .Include(r => r.Nominations)
            .Include(r => r.Votes)
            .FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId && r.ClosedAt == null);

        if (round == null)
        {
            return NotFound("Active voting round not found");
        }

        // Check if user already voted
        if (round.Votes.Any(v => v.UserId == userId.Value))
        {
            return Conflict("User has already voted in this round");
        }

        // Verify recipe is nominated
        if (!round.Nominations.Any(n => n.RecipeId == request.RecipeId))
        {
            return BadRequest("Recipe is not nominated in this round");
        }

        var vote = new VotingVote
        {
            Id = Guid.NewGuid(),
            RoundId = id,
            RecipeId = request.RecipeId,
            UserId = userId.Value,
            VotedAt = DateTime.UtcNow
        };

        _db.VotingVotes.Add(vote);
        await _db.SaveChangesAsync();

        return Created($"/api/voting/rounds/{id}/votes/{vote.Id}", null);
    }

    // POST /api/voting/rounds/{id}/close - Close the voting round (Owner only)
    [HttpPost("rounds/{id:guid}/close")]
    public async Task<ActionResult<VotingRoundDto>> CloseRound(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var membership = await GetUserHouseholdAsync(userId.Value);
        if (membership == null) return BadRequest("User does not belong to a household");
        var (householdId, role) = membership.Value;

        if (role != "Owner")
        {
            return Forbid();
        }

        var round = await _db.VotingRounds
            .Include(r => r.Nominations)
                .ThenInclude(n => n.Recipe)
                    .ThenInclude(r => r.Images)
            .Include(r => r.Votes)
            .FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId && r.ClosedAt == null);

        if (round == null)
        {
            return NotFound("Active voting round not found");
        }

        if (round.Nominations.Count == 0)
        {
            return BadRequest("Cannot close a round with no nominations");
        }

        // Count votes per recipe
        var voteCounts = round.Votes
            .GroupBy(v => v.RecipeId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get recipe IDs with highest votes
        var maxVotes = voteCounts.Values.DefaultIfEmpty(0).Max();
        var topRecipeIds = round.Nominations
            .Select(n => n.RecipeId)
            .Where(rid => voteCounts.GetValueOrDefault(rid, 0) == maxVotes)
            .ToList();

        Guid winnerId;

        if (topRecipeIds.Count == 1)
        {
            winnerId = topRecipeIds[0];
        }
        else
        {
            // Tie-breaker: recipe with lowest cook count wins
            var cookCounts = await _db.CookEvents
                .Where(c => topRecipeIds.Contains(c.RecipeId))
                .GroupBy(c => c.RecipeId)
                .Select(g => new { RecipeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RecipeId, x => x.Count);

            winnerId = topRecipeIds
                .OrderBy(rid => cookCounts.GetValueOrDefault(rid, 0))
                .First();
        }

        round.WinnerId = winnerId;
        round.ClosedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Get user info for nominations
        var nominatorIds = round.Nominations.Select(n => n.NominatedByUserId).Distinct().ToList();
        var users = await _db.Users
            .Where(u => nominatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var winnerRecipe = round.Nominations.First(n => n.RecipeId == winnerId).Recipe;

        var nominations = round.Nominations.Select(n =>
        {
            var titleImage = n.Recipe.Images.FirstOrDefault(i => i.IsTitleImage) ?? n.Recipe.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();
            var voteCount = round.Votes.Count(v => v.RecipeId == n.RecipeId);
            return new NominationDto(
                n.RecipeId,
                n.Recipe.Title,
                titleImage?.Url,
                n.NominatedByUserId,
                users.GetValueOrDefault(n.NominatedByUserId, "Unknown"),
                voteCount
            );
        }).ToList();

        return Ok(new VotingRoundDto(
            round.Id,
            round.CreatedAt,
            round.ClosedAt,
            winnerId,
            winnerRecipe.Title,
            nominations,
            round.Votes.Count,
            round.Votes.Any(v => v.UserId == userId.Value)
        ));
    }

    // GET /api/voting/rounds - Get voting round history
    [HttpGet("rounds")]
    public async Task<ActionResult<PagedResult<VotingRoundSummaryDto>>> GetRoundHistory(
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

        var query = _db.VotingRounds
            .Where(r => r.HouseholdId == householdId && r.ClosedAt != null && r.WinnerId != null);

        var totalCount = await query.CountAsync();

        var rounds = await query
            .OrderByDescending(r => r.ClosedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get winner recipes
        var winnerIds = rounds.Select(r => r.WinnerId!.Value).Distinct().ToList();
        var recipes = await _db.Recipes
            .Where(r => winnerIds.Contains(r.Id))
            .Include(r => r.Images)
            .ToDictionaryAsync(r => r.Id);

        var items = rounds.Select(r =>
        {
            var recipe = recipes.GetValueOrDefault(r.WinnerId!.Value);
            var titleImage = recipe?.Images.FirstOrDefault(i => i.IsTitleImage) ?? recipe?.Images.OrderBy(i => i.OrderIndex).FirstOrDefault();
            return new VotingRoundSummaryDto(
                r.Id,
                r.CreatedAt,
                r.ClosedAt!.Value,
                recipe?.Title ?? "Unknown",
                titleImage?.Url
            );
        }).ToList();

        return Ok(new PagedResult<VotingRoundSummaryDto>(items, totalCount, page, pageSize));
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private async Task<(Guid householdId, string role)?> GetUserHouseholdAsync(Guid userId)
    {
        var member = await _db.HouseholdMembers.FirstOrDefaultAsync(hm => hm.UserId == userId && hm.IsActive);
        return member == null ? null : (member.HouseholdId, member.Role);
    }
}
