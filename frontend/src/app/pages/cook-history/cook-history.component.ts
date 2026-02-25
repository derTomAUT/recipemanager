import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { RecipeService } from '../../services/recipe.service';
import { CookEvent, PagedResult } from '../../models/recipe.model';

@Component({
  selector: 'app-cook-history',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="cook-history-page">
      <header class="page-header">
        <h1>Cook History</h1>
        <p class="subtitle">See what your household has been cooking</p>
      </header>

      <div *ngIf="loading && events.length === 0" class="loading">Loading cook history...</div>
      <div *ngIf="error" class="error">{{ error }}</div>

      <div class="history-feed" *ngIf="events.length > 0">
        <div *ngFor="let event of events" class="cook-event-card">
          <a [routerLink]="['/recipes', event.recipeId]" class="event-link">
            <div class="event-image" *ngIf="event.recipeImageUrl">
              <img [src]="event.recipeImageUrl" [alt]="event.recipeTitle" />
            </div>
            <div class="event-image placeholder" *ngIf="!event.recipeImageUrl">
              <span>No Image</span>
            </div>
            <div class="event-details">
              <h3 class="recipe-title">{{ event.recipeTitle }}</h3>
              <p class="cook-info">
                <span class="user-name">{{ event.userName }}</span>
                cooked this
                <span *ngIf="event.servings"> ({{ event.servings }} servings)</span>
              </p>
              <p class="cook-date">{{ formatDate(event.cookedAt) }}</p>
            </div>
          </a>
        </div>
      </div>

      <div *ngIf="!loading && events.length === 0 && !error" class="empty-state">
        <p>No cook history yet. Start cooking some recipes!</p>
        <a routerLink="/recipes" class="btn">Browse Recipes</a>
      </div>

      <div class="pagination" *ngIf="totalPages > 1">
        <button
          (click)="loadPage(currentPage - 1)"
          [disabled]="currentPage === 1 || loading"
          class="btn">
          Previous
        </button>
        <span class="page-info">Page {{ currentPage }} of {{ totalPages }}</span>
        <button
          (click)="loadPage(currentPage + 1)"
          [disabled]="currentPage >= totalPages || loading"
          class="btn">
          Next
        </button>
      </div>
    </div>
  `,
  styles: [`
    .cook-history-page { padding: 1rem; max-width: 800px; margin: 0 auto; }
    .page-header { margin-bottom: 1.5rem; }
    .page-header h1 { margin: 0 0 0.25rem 0; font-size: 1.75rem; }
    .subtitle { color: #666; margin: 0; }
    .loading { text-align: center; padding: 2rem; color: #666; }
    .error { color: #dc3545; padding: 1rem; background: #f8d7da; border-radius: 4px; margin-bottom: 1rem; }

    .history-feed { display: flex; flex-direction: column; gap: 1rem; }

    .cook-event-card { background: white; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden; transition: box-shadow 0.2s; }
    .cook-event-card:hover { box-shadow: 0 2px 8px rgba(0,0,0,0.1); }

    .event-link { display: flex; text-decoration: none; color: inherit; min-height: 100px; }

    .event-image { width: 120px; min-width: 120px; height: 100px; overflow: hidden; background: #f0f0f0; display: flex; align-items: center; justify-content: center; }
    .event-image img { width: 100%; height: 100%; object-fit: cover; }
    .event-image.placeholder { color: #999; font-size: 0.85rem; }

    .event-details { padding: 0.75rem 1rem; flex: 1; display: flex; flex-direction: column; justify-content: center; }
    .recipe-title { margin: 0 0 0.25rem 0; font-size: 1.1rem; color: #333; }
    .cook-info { margin: 0 0 0.25rem 0; font-size: 0.9rem; color: #666; }
    .user-name { font-weight: 500; color: #007bff; }
    .cook-date { margin: 0; font-size: 0.85rem; color: #999; }

    .empty-state { text-align: center; padding: 3rem 1rem; }
    .empty-state p { color: #666; margin-bottom: 1rem; }

    .btn { padding: 0.75rem 1rem; min-height: 44px; text-decoration: none; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; font-size: 1rem; }
    .btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 2rem; padding: 1rem 0; }
    .page-info { color: #666; }

    @media (max-width: 480px) {
      .event-image { width: 80px; min-width: 80px; height: 80px; }
      .event-details { padding: 0.5rem 0.75rem; }
      .recipe-title { font-size: 1rem; }
    }
  `]
})
export class CookHistoryComponent implements OnInit {
  events: CookEvent[] = [];
  loading = false;
  error = '';
  currentPage = 1;
  totalPages = 1;
  pageSize = 20;

  constructor(private recipeService: RecipeService) {}

  ngOnInit() {
    this.loadPage(1);
  }

  loadPage(page: number) {
    this.loading = true;
    this.error = '';
    this.currentPage = page;

    this.recipeService.getCookHistory({ page, pageSize: this.pageSize }).subscribe({
      next: (result: PagedResult<CookEvent>) => {
        this.events = result.items;
        this.totalPages = Math.ceil(result.totalCount / this.pageSize);
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load cook history';
        this.loading = false;
      }
    });
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays === 0) {
      return 'Today at ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    } else if (diffDays === 1) {
      return 'Yesterday at ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    } else if (diffDays < 7) {
      return diffDays + ' days ago';
    } else {
      return date.toLocaleDateString([], { year: 'numeric', month: 'short', day: 'numeric' });
    }
  }
}
