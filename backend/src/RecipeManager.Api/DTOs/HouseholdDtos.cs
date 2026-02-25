namespace RecipeManager.Api.DTOs;

public record CreateHouseholdRequest(string Name);
public record JoinHouseholdRequest(string InviteCode);
public record HouseholdDto(Guid Id, string Name, string InviteCode, List<MemberDto> Members);
public record MemberDto(Guid Id, string Name, string Email, string Role);
