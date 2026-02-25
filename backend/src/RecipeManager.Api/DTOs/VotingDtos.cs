namespace RecipeManager.Api.DTOs;

public record NominateRequest(Guid RecipeId);
public record VoteRequest(Guid RecipeId);

public record VotingRoundDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    Guid? WinnerId,
    string? WinnerTitle,
    List<NominationDto> Nominations,
    int TotalVotes,
    bool UserHasVoted
);

public record NominationDto(
    Guid RecipeId,
    string RecipeTitle,
    string? RecipeImageUrl,
    Guid NominatedByUserId,
    string NominatedByUserName,
    int VoteCount
);

public record VotingRoundSummaryDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime ClosedAt,
    string WinnerTitle,
    string? WinnerImageUrl
);
