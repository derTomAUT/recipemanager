namespace RecipeManager.Api.DTOs;

public record HouseholdAiSettingsDto(
    string? AiProvider,
    string? AiModel,
    bool HasApiKey,
    double? Latitude,
    double? Longitude
);

public record UpdateHouseholdAiSettingsRequest(
    string? AiProvider,
    string? AiModel,
    string? ApiKey,
    double? Latitude,
    double? Longitude
);

public record UpdateHouseholdLocationRequest(
    double? Latitude,
    double? Longitude
);
