import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { RecipeService } from '../../services/recipe.service';
import { Recipe, PagedResult } from '../../models/recipe.model';

@Component({
  selector: 'app-recipe-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="recipe-list-page">
      <header class="page-header">
        <h1>Recipes</h1>
        <a routerLink="/recipes/new" class="btn-primary">+ New Recipe</a>
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
    </div>
  `,
  styles: [`
    .recipe-list-page { padding: 1rem; max-width: 1200px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .btn-primary { background: #007bff; color: white; padding: 0.5rem 1rem; text-decoration: none; border-radius: 4px; }
    .search-bar { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
    .search-bar input { flex: 1; padding: 0.5rem; font-size: 1rem; }
    .search-bar button { padding: 0.5rem 1rem; }
    .recipe-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 1rem; }
    .recipe-card { border: 1px solid #ddd; border-radius: 8px; overflow: hidden; cursor: pointer; transition: box-shadow 0.2s; }
    .recipe-card:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.1); }
    .recipe-image img { width: 100%; height: 180px; object-fit: cover; }
    .no-image { width: 100%; height: 180px; background: #f0f0f0; display: flex; align-items: center; justify-content: center; color: #999; }
    .recipe-info { padding: 1rem; }
    .recipe-info h3 { margin: 0 0 0.5rem; }
    .description { color: #666; font-size: 0.9rem; margin: 0 0 0.5rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .meta { font-size: 0.85rem; color: #888; display: flex; gap: 1rem; margin-bottom: 0.5rem; }
    .tags { display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .tag { background: #e0e0e0; padding: 0.125rem 0.5rem; border-radius: 4px; font-size: 0.75rem; }
    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; }
    .pagination button { padding: 0.5rem 1rem; }
    .pagination button:disabled { opacity: 0.5; cursor: not-allowed; }
    .empty-state { text-align: center; padding: 2rem; color: #666; }
    .loading { text-align: center; padding: 2rem; }
    .error { color: #dc3545; text-align: center; padding: 1rem; background: #f8d7da; border-radius: 4px; margin-top: 1rem; }
    @media (max-width: 600px) { .recipe-grid { grid-template-columns: 1fr; } }
  `]
})
export class RecipeListComponent implements OnInit {
  result: PagedResult<Recipe> | null = null;
  loading = false;
  error = '';
  searchTerm = '';
  currentPage = 1;
  pageSize = 20;

  constructor(private recipeService: RecipeService) {}

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
      },
      error: () => {
        this.error = 'Failed to load recipes';
        this.loading = false;
      }
    });
  }

  search() {
    this.currentPage = 1;
    this.loadRecipes();
  }

  goToPage(page: number) {
    this.currentPage = page;
    this.loadRecipes();
  }

  get totalPages(): number {
    return this.result ? Math.ceil(this.result.totalCount / this.result.pageSize) : 0;
  }
}
