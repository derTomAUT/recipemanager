namespace RecipeManager.Api.Models;

public class AiDebugLog
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? HouseholdId { get; set; }
    public Guid? UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string RequestJsonSanitized { get; set; } = string.Empty;
    public string ResponseJsonSanitized { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
