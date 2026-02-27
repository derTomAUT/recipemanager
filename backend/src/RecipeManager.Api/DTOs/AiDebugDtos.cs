namespace RecipeManager.Api.DTOs;

public record AiDebugLogDto(
    Guid Id,
    DateTime CreatedAtUtc,
    string Provider,
    string Model,
    string Operation,
    string RequestJsonSanitized,
    string ResponseJsonSanitized,
    int? StatusCode,
    bool Success,
    string? Error
);
