import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { VotingService } from '../../services/voting.service';
import { RecipeService } from '../../services/recipe.service';
import { AuthService } from '../../services/auth.service';
import { VotingRound, VotingRoundSummary } from '../../models/voting.model';
import { Recipe, PagedResult } from '../../models/recipe.model';

@Component({
  selector: 'app-voting',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="voting-page">
      <header class="page-header">
        <h1>Recipe Voting</h1>
        <a routerLink="/home" class="btn-secondary">Back to Home</a>
      </header>

      <div *ngIf="loading" class="loading">Loading...</div>
      <div *ngIf="error" class="error">{{ error }}</div>

      <!-- No Active Round State -->
      <section *ngIf="!loading && !activeRound" class="no-round">
        <div class="empty-card">
          <h2>No Active Voting Round</h2>
          <p>There's no voting round in progress right now.</p>
          <button *ngIf="isOwner" (click)="createRound()" class="btn-primary">Start a New Round</button>
          <p *ngIf="!isOwner" class="info-text">Only household owners can start a new voting round.</p>
        </div>
      </section>

      <!-- Active Round -->
      <section *ngIf="activeRound && !activeRound.closedAt" class="active-round">
        <div class="round-card">
          <h2>Current Voting Round</h2>
          <p class="round-meta">Started {{ formatDate(activeRound.createdAt) }}</p>

          <!-- Winner Announcement (for closed rounds viewed before refresh) -->
          <div *ngIf="activeRound.winnerId" class="winner-banner">
            <h3>Winner: {{ activeRound.winnerTitle }}</h3>
          </div>

          <!-- Nominations -->
          <div class="nominations-section">
            <h3>Nominations ({{ activeRound.nominations.length }}/4)</h3>

            <div *ngIf="activeRound.nominations.length === 0" class="empty-nominations">
              <p>No recipes nominated yet. Be the first to nominate!</p>
            </div>

            <div class="nominations-grid">
              <div *ngFor="let nomination of activeRound.nominations" class="nomination-card">
                <div class="nomination-image">
                  <img *ngIf="nomination.recipeImageUrl" [src]="nomination.recipeImageUrl" [alt]="nomination.recipeTitle" />
                  <div *ngIf="!nomination.recipeImageUrl" class="no-image">No Image</div>
                </div>
                <div class="nomination-info">
                  <h4>{{ nomination.recipeTitle }}</h4>
                  <p class="nominated-by">Nominated by {{ nomination.nominatedByUserName }}</p>
                  <p class="vote-count">{{ nomination.voteCount }} vote{{ nomination.voteCount !== 1 ? 's' : '' }}</p>

                  <div class="nomination-actions">
                    <button
                      *ngIf="!activeRound!.userHasVoted"
                      (click)="vote(nomination.recipeId)"
                      class="btn-vote"
                      [disabled]="voting">
                      Vote
                    </button>
                    <span *ngIf="activeRound!.userHasVoted" class="voted-indicator">
                      {{ userVotedFor === nomination.recipeId ? 'Your vote' : '' }}
                    </span>
                    <button
                      *ngIf="nomination.nominatedByUserId === currentUserId"
                      (click)="withdrawNomination(nomination.recipeId)"
                      class="btn-withdraw"
                      [disabled]="withdrawing">
                      Withdraw
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Nominate Button -->
          <div *ngIf="activeRound.nominations.length < 4" class="nominate-section">
            <button (click)="showRecipePicker = true" class="btn-nominate">+ Nominate a Recipe</button>
          </div>

          <!-- Close Round Button (Owner only) -->
          <div *ngIf="isOwner && activeRound.nominations.length > 0" class="close-round-section">
            <button (click)="closeRound()" class="btn-close" [disabled]="closing">
              {{ closing ? 'Closing...' : 'Close Voting & Pick Winner' }}
            </button>
          </div>

          <!-- Total Votes -->
          <p class="total-votes">Total votes cast: {{ activeRound.totalVotes }}</p>
        </div>
      </section>

      <!-- Closed Round Winner Display -->
      <section *ngIf="activeRound && activeRound.closedAt" class="winner-section">
        <div class="winner-card">
          <h2>Voting Complete!</h2>
          <div class="winner-announcement">
            <span class="trophy">Winner</span>
            <h3>{{ activeRound.winnerTitle }}</h3>
          </div>
          <p class="round-meta">Round closed {{ formatDate(activeRound.closedAt) }}</p>

          <div class="final-results">
            <h4>Final Results</h4>
            <div class="results-list">
              <div *ngFor="let nomination of getSortedNominations()" class="result-item" [class.winner]="nomination.recipeId === activeRound!.winnerId">
                <span class="result-title">{{ nomination.recipeTitle }}</span>
                <span class="result-votes">{{ nomination.voteCount }} vote{{ nomination.voteCount !== 1 ? 's' : '' }}</span>
              </div>
            </div>
          </div>

          <button *ngIf="isOwner" (click)="startNewRound()" class="btn-primary">Start New Round</button>
        </div>
      </section>

      <!-- Recipe Picker Modal -->
      <div *ngIf="showRecipePicker" class="modal-overlay" (click)="showRecipePicker = false">
        <div class="modal-content" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>Select a Recipe to Nominate</h3>
            <button (click)="showRecipePicker = false" class="btn-close-modal">&times;</button>
          </div>
          <div class="modal-body">
            <div *ngIf="loadingRecipes" class="loading">Loading recipes...</div>
            <div *ngIf="recipesError" class="error">{{ recipesError }}</div>
            <div class="recipe-picker-grid">
              <div *ngFor="let recipe of availableRecipes"
                   class="picker-recipe"
                   (click)="nominateRecipe(recipe.id)"
                   [class.disabled]="isAlreadyNominated(recipe.id)">
                <div class="picker-image">
                  <img *ngIf="recipe.titleImageUrl" [src]="recipe.titleImageUrl" [alt]="recipe.title" />
                  <div *ngIf="!recipe.titleImageUrl" class="no-image">No Image</div>
                </div>
                <div class="picker-info">
                  <h4>{{ recipe.title }}</h4>
                  <span *ngIf="isAlreadyNominated(recipe.id)" class="already-nominated">Already nominated</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Past Rounds History -->
      <section class="history-section">
        <h2>Past Voting Rounds</h2>
        <div *ngIf="loadingHistory" class="loading">Loading history...</div>

        <div *ngIf="history && history.items.length > 0" class="history-grid">
          <div *ngFor="let round of history.items" class="history-card">
            <div class="history-image">
              <img *ngIf="round.winnerImageUrl" [src]="round.winnerImageUrl" [alt]="round.winnerTitle" />
              <div *ngIf="!round.winnerImageUrl" class="no-image">No Image</div>
            </div>
            <div class="history-info">
              <h4>{{ round.winnerTitle }}</h4>
              <p class="history-date">{{ formatDate(round.closedAt) }}</p>
            </div>
          </div>
        </div>

        <div *ngIf="history && history.items.length === 0 && !loadingHistory" class="empty-history">
          <p>No completed voting rounds yet.</p>
        </div>

        <div class="pagination" *ngIf="history && history.totalCount > history.pageSize">
          <button [disabled]="historyPage <= 1" (click)="loadHistory(historyPage - 1)">Previous</button>
          <span>Page {{ historyPage }} of {{ totalHistoryPages }}</span>
          <button [disabled]="historyPage >= totalHistoryPages" (click)="loadHistory(historyPage + 1)">Next</button>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .voting-page { padding: 1rem; max-width: 900px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; flex-wrap: wrap; gap: 1rem; }
    .page-header h1 { margin: 0; }
    .btn-secondary { padding: 0.5rem 1rem; min-height: 44px; display: inline-flex; align-items: center; background: #6c757d; color: white; text-decoration: none; border-radius: 4px; }
    .btn-primary { padding: 0.75rem 1.5rem; min-height: 44px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 1rem; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }

    .loading { text-align: center; padding: 2rem; }
    .error { background: #f8d7da; color: #721c24; padding: 1rem; border-radius: 4px; margin-bottom: 1rem; }

    .no-round, .active-round, .winner-section { margin-bottom: 2rem; }
    .empty-card, .round-card, .winner-card { background: #fff; border: 1px solid #ddd; border-radius: 8px; padding: 1.5rem; }
    .empty-card { text-align: center; }
    .empty-card h2 { margin-top: 0; }
    .info-text { color: #666; font-style: italic; }

    .round-meta { color: #666; font-size: 0.9rem; margin-bottom: 1rem; }

    .winner-banner { background: #d4edda; border: 1px solid #c3e6cb; border-radius: 4px; padding: 1rem; margin-bottom: 1rem; text-align: center; }
    .winner-banner h3 { margin: 0; color: #155724; }

    .nominations-section { margin-bottom: 1.5rem; }
    .nominations-section h3 { margin-bottom: 1rem; }
    .empty-nominations { color: #666; font-style: italic; padding: 1rem; background: #f8f9fa; border-radius: 4px; }

    .nominations-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 1rem; }
    .nomination-card { border: 1px solid #ddd; border-radius: 8px; overflow: hidden; background: #fff; }
    .nomination-image img { width: 100%; height: 120px; object-fit: cover; }
    .nomination-image .no-image { width: 100%; height: 120px; background: #f0f0f0; display: flex; align-items: center; justify-content: center; color: #999; }
    .nomination-info { padding: 0.75rem; }
    .nomination-info h4 { margin: 0 0 0.25rem; font-size: 1rem; }
    .nominated-by { font-size: 0.8rem; color: #666; margin: 0 0 0.25rem; }
    .vote-count { font-size: 0.85rem; color: #007bff; font-weight: 600; margin: 0 0 0.5rem; }

    .nomination-actions { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .btn-vote { padding: 0.5rem 1rem; min-height: 44px; background: #28a745; color: white; border: none; border-radius: 4px; cursor: pointer; }
    .btn-vote:disabled { opacity: 0.6; cursor: not-allowed; }
    .btn-withdraw { padding: 0.5rem 0.75rem; min-height: 44px; background: #dc3545; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 0.85rem; }
    .btn-withdraw:disabled { opacity: 0.6; cursor: not-allowed; }
    .voted-indicator { font-size: 0.85rem; color: #28a745; font-weight: 600; }

    .nominate-section { margin-bottom: 1rem; }
    .btn-nominate { padding: 0.75rem 1.5rem; min-height: 44px; background: #17a2b8; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 1rem; }

    .close-round-section { margin-bottom: 1rem; }
    .btn-close { padding: 0.75rem 1.5rem; min-height: 44px; background: #ffc107; color: #212529; border: none; border-radius: 4px; cursor: pointer; font-size: 1rem; }
    .btn-close:disabled { opacity: 0.6; cursor: not-allowed; }

    .total-votes { color: #666; font-size: 0.9rem; margin-top: 1rem; }

    .winner-card { text-align: center; }
    .winner-card h2 { margin-top: 0; }
    .winner-announcement { background: linear-gradient(135deg, #ffd700 0%, #ffb900 100%); padding: 1.5rem; border-radius: 8px; margin-bottom: 1rem; }
    .trophy { display: inline-block; background: #fff; color: #b8860b; padding: 0.25rem 0.75rem; border-radius: 4px; font-size: 0.85rem; font-weight: 600; margin-bottom: 0.5rem; }
    .winner-announcement h3 { margin: 0; font-size: 1.5rem; color: #fff; text-shadow: 0 1px 2px rgba(0,0,0,0.2); }

    .final-results { background: #f8f9fa; border-radius: 8px; padding: 1rem; margin-bottom: 1.5rem; text-align: left; }
    .final-results h4 { margin: 0 0 0.75rem; }
    .results-list { display: flex; flex-direction: column; gap: 0.5rem; }
    .result-item { display: flex; justify-content: space-between; padding: 0.5rem; background: #fff; border-radius: 4px; border: 1px solid #ddd; }
    .result-item.winner { background: #d4edda; border-color: #c3e6cb; }
    .result-title { font-weight: 500; }
    .result-votes { color: #666; }

    /* Modal */
    .modal-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000; padding: 1rem; }
    .modal-content { background: #fff; border-radius: 8px; width: 100%; max-width: 600px; max-height: 80vh; overflow: hidden; display: flex; flex-direction: column; }
    .modal-header { display: flex; justify-content: space-between; align-items: center; padding: 1rem; border-bottom: 1px solid #ddd; }
    .modal-header h3 { margin: 0; }
    .btn-close-modal { background: none; border: none; font-size: 1.5rem; cursor: pointer; padding: 0.25rem 0.5rem; }
    .modal-body { padding: 1rem; overflow-y: auto; }

    .recipe-picker-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 0.75rem; }
    .picker-recipe { border: 1px solid #ddd; border-radius: 8px; overflow: hidden; cursor: pointer; transition: box-shadow 0.2s; }
    .picker-recipe:hover:not(.disabled) { box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
    .picker-recipe.disabled { opacity: 0.5; cursor: not-allowed; }
    .picker-image img { width: 100%; height: 80px; object-fit: cover; }
    .picker-image .no-image { width: 100%; height: 80px; background: #f0f0f0; display: flex; align-items: center; justify-content: center; color: #999; font-size: 0.75rem; }
    .picker-info { padding: 0.5rem; }
    .picker-info h4 { margin: 0; font-size: 0.9rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .already-nominated { font-size: 0.75rem; color: #dc3545; }

    /* History */
    .history-section { margin-top: 2rem; }
    .history-section h2 { margin-bottom: 1rem; }
    .history-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 1rem; }
    .history-card { border: 1px solid #ddd; border-radius: 8px; overflow: hidden; }
    .history-image img { width: 100%; height: 100px; object-fit: cover; }
    .history-image .no-image { width: 100%; height: 100px; background: #f0f0f0; display: flex; align-items: center; justify-content: center; color: #999; }
    .history-info { padding: 0.75rem; }
    .history-info h4 { margin: 0 0 0.25rem; font-size: 1rem; }
    .history-date { margin: 0; font-size: 0.85rem; color: #666; }
    .empty-history { color: #666; font-style: italic; }

    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; }
    .pagination button { padding: 0.5rem 1rem; min-height: 44px; }
    .pagination button:disabled { opacity: 0.5; cursor: not-allowed; }

    @media (max-width: 600px) {
      .nominations-grid { grid-template-columns: 1fr; }
      .recipe-picker-grid { grid-template-columns: repeat(2, 1fr); }
      .history-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class VotingComponent implements OnInit {
  activeRound: VotingRound | null = null;
  history: PagedResult<VotingRoundSummary> | null = null;
  availableRecipes: Recipe[] = [];

  loading = false;
  loadingHistory = false;
  loadingRecipes = false;
  voting = false;
  withdrawing = false;
  closing = false;

  error = '';
  recipesError = '';

  showRecipePicker = false;
  historyPage = 1;

  currentUserId = '';
  isOwner = false;
  userVotedFor: string | null = null;

  constructor(
    private votingService: VotingService,
    private recipeService: RecipeService,
    private authService: AuthService
  ) {}

  ngOnInit() {
    this.authService.user$.subscribe(user => {
      if (user) {
        this.currentUserId = user.id;
        this.isOwner = user.role === 'Owner';
      }
    });
    this.loadActiveRound();
    this.loadHistory(1);
  }

  loadActiveRound() {
    this.loading = true;
    this.error = '';
    this.votingService.getActiveRound().subscribe({
      next: (round) => {
        this.activeRound = round;
        this.loading = false;
        if (round && round.userHasVoted) {
          // Try to find what the user voted for (not available from API, would need extra logic)
          // For now, just mark that they voted
        }
      },
      error: () => {
        this.error = 'Failed to load voting round';
        this.loading = false;
      }
    });
  }

  loadHistory(page: number) {
    this.loadingHistory = true;
    this.historyPage = page;
    this.votingService.getRoundHistory({ page, pageSize: 10 }).subscribe({
      next: (result) => {
        this.history = result;
        this.loadingHistory = false;
      },
      error: () => {
        this.loadingHistory = false;
      }
    });
  }

  get totalHistoryPages(): number {
    return this.history ? Math.ceil(this.history.totalCount / this.history.pageSize) : 0;
  }

  createRound() {
    this.loading = true;
    this.votingService.createRound().subscribe({
      next: (round) => {
        this.activeRound = round;
        this.loading = false;
      },
      error: (err) => {
        if (err.status === 409) {
          this.error = 'An active voting round already exists';
        } else if (err.status === 403) {
          this.error = 'Only household owners can create voting rounds';
        } else {
          this.error = 'Failed to create voting round';
        }
        this.loading = false;
      }
    });
  }

  startNewRound() {
    this.activeRound = null;
    this.createRound();
  }

  loadRecipesForPicker() {
    if (this.availableRecipes.length > 0) return;

    this.loadingRecipes = true;
    this.recipesError = '';
    this.recipeService.getRecipes({ pageSize: 100 }).subscribe({
      next: (result) => {
        this.availableRecipes = result.items;
        this.loadingRecipes = false;
      },
      error: () => {
        this.recipesError = 'Failed to load recipes';
        this.loadingRecipes = false;
      }
    });
  }

  isAlreadyNominated(recipeId: string): boolean {
    if (!this.activeRound) return false;
    return this.activeRound.nominations.some(n => n.recipeId === recipeId);
  }

  nominateRecipe(recipeId: string) {
    if (!this.activeRound || this.isAlreadyNominated(recipeId)) return;

    this.votingService.nominate(this.activeRound.id, recipeId).subscribe({
      next: (nomination) => {
        this.activeRound!.nominations.push(nomination);
        this.showRecipePicker = false;
      },
      error: (err) => {
        if (err.status === 409) {
          this.error = 'Maximum nominations reached or recipe already nominated';
        } else {
          this.error = 'Failed to nominate recipe';
        }
      }
    });
  }

  vote(recipeId: string) {
    if (!this.activeRound || this.activeRound.userHasVoted) return;

    this.voting = true;
    this.votingService.vote(this.activeRound.id, recipeId).subscribe({
      next: () => {
        this.activeRound!.userHasVoted = true;
        this.activeRound!.totalVotes++;
        this.userVotedFor = recipeId;
        const nomination = this.activeRound!.nominations.find(n => n.recipeId === recipeId);
        if (nomination) {
          nomination.voteCount++;
        }
        this.voting = false;
      },
      error: (err) => {
        if (err.status === 409) {
          this.error = 'You have already voted in this round';
          this.activeRound!.userHasVoted = true;
        } else {
          this.error = 'Failed to cast vote';
        }
        this.voting = false;
      }
    });
  }

  withdrawNomination(recipeId: string) {
    if (!this.activeRound) return;

    this.withdrawing = true;
    this.votingService.withdrawNomination(this.activeRound.id, recipeId).subscribe({
      next: () => {
        const idx = this.activeRound!.nominations.findIndex(n => n.recipeId === recipeId);
        if (idx !== -1) {
          const nomination = this.activeRound!.nominations[idx];
          this.activeRound!.totalVotes -= nomination.voteCount;
          this.activeRound!.nominations.splice(idx, 1);
        }
        this.withdrawing = false;
      },
      error: () => {
        this.error = 'Failed to withdraw nomination';
        this.withdrawing = false;
      }
    });
  }

  closeRound() {
    if (!this.activeRound) return;

    this.closing = true;
    this.votingService.closeRound(this.activeRound.id).subscribe({
      next: (round) => {
        this.activeRound = round;
        this.closing = false;
        this.loadHistory(1); // Refresh history
      },
      error: (err) => {
        if (err.status === 403) {
          this.error = 'Only household owners can close voting rounds';
        } else {
          this.error = 'Failed to close voting round';
        }
        this.closing = false;
      }
    });
  }

  getSortedNominations() {
    if (!this.activeRound) return [];
    return [...this.activeRound.nominations].sort((a, b) => b.voteCount - a.voteCount);
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  // Called when opening the recipe picker modal
  ngDoCheck() {
    if (this.showRecipePicker && this.availableRecipes.length === 0 && !this.loadingRecipes) {
      this.loadRecipesForPicker();
    }
  }
}
