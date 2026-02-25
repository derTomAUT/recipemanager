export interface VotingRound {
  id: string;
  createdAt: string;
  closedAt?: string;
  winnerId?: string;
  winnerTitle?: string;
  nominations: Nomination[];
  totalVotes: number;
  userHasVoted: boolean;
}

export interface Nomination {
  recipeId: string;
  recipeTitle: string;
  recipeImageUrl?: string;
  nominatedByUserId: string;
  nominatedByUserName: string;
  voteCount: number;
}

export interface VotingRoundSummary {
  id: string;
  createdAt: string;
  closedAt: string;
  winnerTitle: string;
  winnerImageUrl?: string;
}
