using System.ComponentModel.DataAnnotations;

namespace RecipeManager.Api.DTOs;

public record CreateHouseholdRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name
);

public record JoinHouseholdRequest(
    [Required, StringLength(20, MinimumLength = 1)] string InviteCode
);

public record HouseholdDto(Guid Id, string Name, string InviteCode, List<MemberDto> Members);
public record MemberDto(Guid Id, string Name, string Email, string Role, bool IsActive);

public record HouseholdInviteDto(
    string InviteCode,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsExpired
);

public record HouseholdActivityDto(
    Guid Id,
    string EventType,
    Guid ActorUserId,
    Guid? TargetUserId,
    string? Details,
    DateTime CreatedAtUtc
);

public record UpdateHouseholdMemberRoleRequest(
    [Required, StringLength(20, MinimumLength = 1)] string Role
);
