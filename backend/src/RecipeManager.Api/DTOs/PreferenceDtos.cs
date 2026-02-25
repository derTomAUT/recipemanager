namespace RecipeManager.Api.DTOs;

public record PreferencesDto(
    string[] Allergens,
    string[] DislikedIngredients,
    string[] FavoriteCuisines
);

public record UpdatePreferencesRequest(
    string[] Allergens,
    string[] DislikedIngredients,
    string[] FavoriteCuisines
);
