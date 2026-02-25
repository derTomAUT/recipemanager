namespace RecipeManager.Api.DTOs;

public record GoogleLoginRequest(string IdToken);
public record AuthResponse(string Token, UserDto User);
public record UserDto(Guid Id, string Email, string Name, string? ProfileImageUrl, Guid? HouseholdId, string? Role);
