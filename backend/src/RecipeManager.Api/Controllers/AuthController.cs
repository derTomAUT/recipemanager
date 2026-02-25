using System.Security.Claims;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Infrastructure.Auth;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly GoogleTokenValidator _googleValidator;
    private readonly JwtTokenGenerator _jwtGenerator;

    public AuthController(AppDbContext db, GoogleTokenValidator googleValidator, JwtTokenGenerator jwtGenerator)
    {
        _db = db;
        _googleValidator = googleValidator;
        _jwtGenerator = jwtGenerator;
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await _googleValidator.ValidateAsync(request.IdToken);
        }
        catch (InvalidJwtException)
        {
            return Unauthorized();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                GoogleId = payload.Subject,
                Email = payload.Email,
                Name = payload.Name,
                ProfileImageUrl = payload.Picture
            };
            _db.Users.Add(user);
        }
        else
        {
            // Update profile data from Google on each login
            user.Email = payload.Email;
            user.Name = payload.Name;
            user.ProfileImageUrl = payload.Picture;
        }
        await _db.SaveChangesAsync();

        // Get household membership (User doesn't have HouseholdMembers navigation, query separately)
        var membership = await _db.HouseholdMembers.FirstOrDefaultAsync(hm => hm.UserId == user.Id);

        var token = _jwtGenerator.Generate(
            user.Id,
            user.Email,
            membership?.HouseholdId,
            membership?.Role
        );

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.ProfileImageUrl,
            membership?.HouseholdId,
            membership?.Role
        );

        return Ok(new AuthResponse(token, userDto));
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> RefreshToken()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized();

        var membership = await _db.HouseholdMembers.FirstOrDefaultAsync(hm => hm.UserId == userId);

        var token = _jwtGenerator.Generate(
            user.Id,
            user.Email,
            membership?.HouseholdId,
            membership?.Role
        );

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.ProfileImageUrl,
            membership?.HouseholdId,
            membership?.Role
        );

        return Ok(new AuthResponse(token, userDto));
    }
}
