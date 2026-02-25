namespace RecipeManager.Api.Models;

public class VotingRound
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public Guid? WinnerId { get; set; }
    public Household Household { get; set; } = null!;
    public ICollection<VotingNomination> Nominations { get; set; } = new List<VotingNomination>();
    public ICollection<VotingVote> Votes { get; set; } = new List<VotingVote>();
}

public class VotingNomination
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public Guid RecipeId { get; set; }
    public Guid NominatedByUserId { get; set; }
    public DateTime NominatedAt { get; set; } = DateTime.UtcNow;
    public VotingRound Round { get; set; } = null!;
    public Recipe Recipe { get; set; } = null!;
}

public class VotingVote
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public Guid RecipeId { get; set; }
    public Guid UserId { get; set; }
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
    public VotingRound Round { get; set; } = null!;
}
