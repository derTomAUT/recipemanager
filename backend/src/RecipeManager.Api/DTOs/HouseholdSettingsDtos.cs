namespace RecipeManager.Api.DTOs;

public record HouseholdAiSettingsDto(
    string? AiProvider,
    string? AiModel,
    bool HasApiKey
);

public record UpdateHouseholdAiSettingsRequest(
    string? AiProvider,
    string? AiModel,
    string? ApiKey
);
