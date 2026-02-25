namespace RecipeManager.Api.DTOs;

public record MarkCookedRequest(int? Servings);

public record CookEventDto(
    Guid Id,
    Guid RecipeId,
    string RecipeTitle,
    string? RecipeImageUrl,
    Guid UserId,
    string UserName,
    DateTime CookedAt,
    int? Servings
);
