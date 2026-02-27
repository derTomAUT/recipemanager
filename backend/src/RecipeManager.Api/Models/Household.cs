namespace RecipeManager.Api.Models;

public class Household
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AiProvider { get; set; }
    public string? AiModel { get; set; }
    public string? AiApiKeyEncrypted { get; set; }
    public ICollection<HouseholdMember> Members { get; set; } = new List<HouseholdMember>();
}

public class HouseholdMember
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public string Role { get; set; } = "Member"; // Owner, Member, Viewer
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public Household Household { get; set; } = null!;
    public User User { get; set; } = null!;
}
