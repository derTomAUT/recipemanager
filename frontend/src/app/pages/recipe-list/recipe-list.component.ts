import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { RecipeService } from '../../services/recipe.service';
import { Recipe, PagedResult } from '../../models/recipe.model';
import { RecipeImportService } from '../../services/recipe-import.service';
import { RecipeDraftService } from '../../services/recipe-draft.service';
import { getHttpErrorMessage } from '../../utils/http-error.utils';

@Component({
  selector: 'app-recipe-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="recipe-list-page">
      <header class="page-header">
        <h1>Recipes</h1>
        <div class="page-actions">
          <button class="btn btn-primary" type="button" (click)="openImportModal()">Import Recipe</button>
          <a routerLink="/import/paper-card" class="btn btn-secondary">Import from Paper Card</a>
          <a routerLink="/recipes/new" class="btn btn-secondary">New Recipe</a>
        </div>
      </header>

      <div class="search-bar">
        <input
          type="text"
          [(ngModel)]="searchTerm"
          (keyup.enter)="search()"
          placeholder="Search recipes..."
        />
        <button (click)="search()">Search</button>
      </div>

      <div class="recipe-grid" *ngIf="result">
        <div *ngFor="let recipe of result.items" class="recipe-card" [routerLink]="['/recipes', recipe.id]">
          <button class="favorite-btn" [class.favorited]="isFavorite(recipe.id)" (click)="toggleFavorite($event, recipe.id)" aria-label="Toggle favorite">
            {{ isFavorite(recipe.id) ? '&hearts;' : '&hearts;' }}
          </button>
          <div class="recipe-image">
            <img *ngIf="recipe.titleImageUrl" [src]="recipe.titleImageUrl" [alt]="recipe.title" />
            <div *ngIf="!recipe.titleImageUrl" class="no-image">No Image</div>
          </div>
          <div class="recipe-info">
            <h3>{{ recipe.title }}</h3>
            <p *ngIf="recipe.description" class="description">{{ recipe.description }}</p>
            <div class="meta">
              <span *ngIf="recipe.prepMinutes || recipe.cookMinutes">
                {{ (recipe.prepMinutes || 0) + (recipe.cookMinutes || 0) }} min
              </span>
              <span *ngIf="recipe.servings">{{ recipe.servings }} servings</span>
              <span *ngIf="recipe.cookCount">Cooked {{ recipe.cookCount }}x</span>
            </div>
            <div class="tags" *ngIf="recipe.tags.length">
              <span *ngFor="let tag of recipe.tags" class="tag">{{ tag }}</span>
            </div>
          </div>
        </div>
      </div>

      <div *ngIf="result && result.items.length === 0" class="empty-state">
        <p>No recipes found. <a routerLink="/recipes/new">Create your first recipe!</a></p>
      </div>

      <div class="pagination" *ngIf="result && result.totalCount > result.pageSize">
        <button [disabled]="currentPage <= 1" (click)="goToPage(currentPage - 1)">Previous</button>
        <span>Page {{ currentPage }} of {{ totalPages }}</span>
        <button [disabled]="currentPage >= totalPages" (click)="goToPage(currentPage + 1)">Next</button>
      </div>

      <div *ngIf="loading" class="loading">Loading...</div>
      <div *ngIf="error" class="error">{{ error }}</div>

      <div class="modal-backdrop" *ngIf="showImportModal" (click)="closeImportModal()">
        <div class="import-modal" role="dialog" aria-modal="true" aria-labelledby="import-modal-title" (click)="$event.stopPropagation()">
          <h3 id="import-modal-title">Import Recipe From URL</h3>
          <p class="modal-subtitle">Paste a recipe URL to import and open it in the editor as a draft.</p>
          <input
            type="url"
            [(ngModel)]="importUrl"
            [disabled]="importing"
            placeholder="https://example.com/recipe"
            (keyup.enter)="importFromUrl()"
          />
          <div *ngIf="importError" class="modal-error">{{ importError }}</div>
          <div class="modal-actions">
            <button class="btn btn-secondary" type="button" (click)="closeImportModal()" [disabled]="importing">Cancel</button>
            <button class="btn btn-primary" type="button" (click)="importFromUrl()" [disabled]="importing">
              <span *ngIf="importing" class="spinner" aria-hidden="true"></span>
              {{ importing ? 'Importing...' : 'Import' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .recipe-list-page { padding: 1rem; max-width: 1200px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; gap: 0.75rem; flex-wrap: wrap; }
    .page-actions { display: flex; gap: 0.6rem; flex-wrap: wrap; }
    .btn { border: none; border-radius: var(--radius-sm); padding: 0.6rem 1rem; min-height: 44px; cursor: pointer; text-decoration: none; display: inline-flex; align-items: center; justify-content: center; gap: 0.45rem; }
    .btn-primary { background: var(--primary); color: white; }
    .btn-secondary { background: var(--surface-2); color: var(--text); }
    .search-bar { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
    .search-bar input { flex: 1; padding: 0.5rem; font-size: 1rem; min-height: 44px; border: 1px solid color-mix(in srgb, var(--text) 18%, transparent); border-radius: var(--radius-sm); background: var(--surface-2); color: var(--text); }
    .search-bar button { padding: 0.75rem 1rem; min-height: 44px; border-radius: var(--radius-sm); border: none; background: var(--surface-2); color: var(--text); }
    .spinner { width: 14px; height: 14px; border: 2px solid rgba(255,255,255,0.5); border-top-color: currentColor; border-radius: 50%; animation: spin 0.8s linear infinite; }
    .recipe-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 1rem; }
    .recipe-card { position: relative; border: 1px solid color-mix(in srgb, var(--text) 14%, transparent); border-radius: var(--radius-md); overflow: hidden; cursor: pointer; transition: box-shadow 0.2s; background: var(--surface); }
    .recipe-card:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.1); }
    .favorite-btn { position: absolute; top: 8px; right: 8px; background: white; border: none; border-radius: 50%; width: 44px; height: 44px; font-size: 1.25rem; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1); z-index: 1; color: #ccc; }
    .favorite-btn.favorited { color: #dc3545; }
    .recipe-image img { width: 100%; height: 180px; object-fit: cover; }
    .no-image { width: 100%; height: 180px; background: #f0f0f0; display: flex; align-items: center; justify-content: center; color: #999; }
    .recipe-info { padding: 1rem; }
    .recipe-info h3 { margin: 0 0 0.5rem; }
    .description { color: #666; font-size: 0.9rem; margin: 0 0 0.5rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .meta { font-size: 0.85rem; color: #888; display: flex; gap: 1rem; margin-bottom: 0.5rem; }
    .tags { display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .tag { background: var(--surface-2); color: var(--muted); padding: 0.125rem 0.5rem; border-radius: 999px; font-size: 0.75rem; }
    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; }
    .pagination button { padding: 0.75rem 1rem; min-height: 44px; border-radius: var(--radius-sm); border: none; background: var(--surface-2); color: var(--text); }
    .pagination button:disabled { opacity: 0.5; cursor: not-allowed; }
    .empty-state { text-align: center; padding: 2rem; color: #666; }
    .loading { text-align: center; padding: 2rem; }
    .error { color: var(--text); text-align: center; padding: 1rem; background: color-mix(in srgb, var(--primary) 20%, var(--surface)); border-radius: var(--radius-sm); margin-top: 1rem; }
    .modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; z-index: 1200; }
    .import-modal { width: min(560px, 100%); background: var(--surface); border-radius: var(--radius-lg); box-shadow: var(--shadow); padding: 1.25rem; border: 1px solid rgba(0,0,0,0.08); }
    .import-modal h3 { margin: 0 0 0.5rem; }
    .modal-subtitle { margin: 0 0 0.9rem; color: var(--muted); font-size: 0.95rem; }
    .import-modal input { width: 100%; min-height: 44px; padding: 0.5rem; border: 1px solid color-mix(in srgb, var(--text) 18%, transparent); border-radius: var(--radius-sm); background: var(--surface-2); color: var(--text); }
    .modal-actions { margin-top: 1rem; display: flex; justify-content: flex-end; gap: 0.6rem; }
    .modal-error { margin-top: 0.65rem; background: color-mix(in srgb, var(--primary) 20%, var(--surface)); color: var(--text); border-radius: var(--radius-sm); padding: 0.55rem 0.7rem; font-size: 0.9rem; }
    @keyframes spin { to { transform: rotate(360deg); } }
    @media (max-width: 600px) { .recipe-grid { grid-template-columns: 1fr; } }
  `]
})
export class RecipeListComponent implements OnInit {
  result: PagedResult<Recipe> | null = null;
  loading = false;
  error = '';
  importUrl = '';
  importing = false;
  importError = '';
  showImportModal = false;
  searchTerm = '';
  currentPage = 1;
  pageSize = 20;
  favoriteIds: Set<string> = new Set();

  constructor(
    private recipeService: RecipeService,
    private recipeImportService: RecipeImportService,
    private recipeDraftService: RecipeDraftService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadRecipes();
  }

  loadRecipes() {
    this.loading = true;
    this.error = '';
    this.recipeService.getRecipes({
      search: this.searchTerm || undefined,
      page: this.currentPage,
      pageSize: this.pageSize
    }).subscribe({
      next: (data) => {
        this.result = data;
        this.loading = false;
        this.cdr.detectChanges();
        this.loadFavorites();
      },
      error: () => {
        this.error = 'Failed to load recipes';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  search() {
    this.currentPage = 1;
    this.loadRecipes();
  }

  importFromUrl() {
    if (!this.importUrl.trim()) {
      this.importError = 'Please enter a URL.';
      return;
    }

    this.importing = true;
    this.importError = '';
    this.recipeImportService.importFromUrl(this.importUrl.trim()).subscribe({
      next: (draft) => {
        this.recipeDraftService.setDraft(draft);
        this.importing = false;
        this.showImportModal = false;
        this.importUrl = '';
        this.router.navigate(['/recipes/new']);
      },
      error: (error) => {
        this.importError = getHttpErrorMessage(error, 'Failed to import recipe from URL.');
        this.importing = false;
      }
    });
  }

  openImportModal() {
    this.showImportModal = true;
    this.importError = '';
  }

  closeImportModal() {
    if (this.importing) return;
    this.showImportModal = false;
    this.importError = '';
  }

  goToPage(page: number) {
    this.currentPage = page;
    this.loadRecipes();
  }

  get totalPages(): number {
    return this.result ? Math.ceil(this.result.totalCount / this.result.pageSize) : 0;
  }

  loadFavorites() {
    this.recipeService.getFavorites({ pageSize: 100 }).subscribe({
      next: (data) => {
        this.favoriteIds = new Set(data.items.map(r => r.id));
        this.cdr.detectChanges();
      }
    });
  }

  isFavorite(id: string): boolean {
    return this.favoriteIds.has(id);
  }

  toggleFavorite(event: Event, id: string) {
    event.stopPropagation();
    if (this.favoriteIds.has(id)) {
      this.recipeService.removeFavorite(id).subscribe({
        next: () => {
          this.favoriteIds.delete(id);
          this.cdr.detectChanges();
        },
        error: () => {
          this.error = 'Failed to update favorite';
          this.cdr.detectChanges();
        }
      });
    } else {
      this.recipeService.addFavorite(id).subscribe({
        next: () => {
          this.favoriteIds.add(id);
          this.cdr.detectChanges();
        },
        error: () => {
          this.error = 'Failed to update favorite';
          this.cdr.detectChanges();
        }
      });
    }
  }
}
