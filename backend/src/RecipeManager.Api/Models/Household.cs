namespace RecipeManager.Api.Models;

public class Household
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public DateTime InviteCodeCreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime InviteCodeExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(5);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AiProvider { get; set; }
    public string? AiModel { get; set; }
    public string? AiApiKeyEncrypted { get; set; }
    public ICollection<HouseholdMember> Members { get; set; } = new List<HouseholdMember>();
    public ICollection<HouseholdInvite> Invites { get; set; } = new List<HouseholdInvite>();
    public ICollection<HouseholdActivityLog> ActivityLogs { get; set; } = new List<HouseholdActivityLog>();
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

public class HouseholdInvite
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string InviteCode { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(5);
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }

    public Household Household { get; set; } = null!;
}

public class HouseholdActivityLog
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Household Household { get; set; } = null!;
}
