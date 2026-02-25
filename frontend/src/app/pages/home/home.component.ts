import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { RecipeService } from '../../services/recipe.service';
import { AuthService } from '../../services/auth.service';
import { Recipe } from '../../models/recipe.model';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="home-page">
      <header class="page-header">
        <h1>Welcome to Recipe Manager</h1>
        <div class="nav-links">
          <a routerLink="/recipes" class="btn">All Recipes</a>
          <a routerLink="/preferences" class="btn">My Preferences</a>
          <a routerLink="/logs" class="btn">Logs</a>
          <a *ngIf="isOwner" routerLink="/household/settings" class="btn">Household Settings</a>
        </div>
      </header>

      <section class="recommended-section">
        <h2>Recommended For You</h2>
        <div *ngIf="loading" class="loading">Loading recommendations...</div>
        <div *ngIf="error" class="error">{{ error }}</div>

        <div class="recipe-grid" *ngIf="recommended.length > 0">
          <div *ngFor="let recipe of recommended" class="recipe-card" [routerLink]="['/recipes', recipe.id]">
            <div class="recipe-image">
              <img *ngIf="recipe.titleImageUrl" [src]="recipe.titleImageUrl" [alt]="recipe.title" />
              <div *ngIf="!recipe.titleImageUrl" class="no-image">No Image</div>
            </div>
            <div class="recipe-info">
              <h3>{{ recipe.title }}</h3>
              <p *ngIf="recipe.description" class="description">{{ recipe.description }}</p>
              <div class="meta">
                <span *ngIf="recipe.prepMinutes || recipe.cookMinutes">{{ (recipe.prepMinutes || 0) + (recipe.cookMinutes || 0) }} min</span>
                <span *ngIf="recipe.cookCount">Cooked {{ recipe.cookCount }}x</span>
              </div>
            </div>
          </div>
        </div>

        <div *ngIf="!loading && recommended.length === 0" class="empty-state">
          <p>No recommendations yet. <a routerLink="/recipes/new">Add some recipes</a> or <a routerLink="/preferences">set your preferences</a>!</p>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .home-page { padding: 1rem; max-width: 1200px; margin: 0 auto; }
    .page-header { margin-bottom: 2rem; }
    .page-header h1 { margin: 0 0 1rem; }
    .nav-links { display: flex; gap: 1rem; flex-wrap: wrap; }
    .btn { padding: 0.75rem 1rem; min-height: 44px; display: inline-flex; align-items: center; background: #007bff; color: white; text-decoration: none; border-radius: 4px; }
    .recommended-section h2 { margin-bottom: 1rem; }
    .recipe-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 1rem; }
    .recipe-card { border: 1px solid #ddd; border-radius: 8px; overflow: hidden; cursor: pointer; transition: box-shadow 0.2s; }
    .recipe-card:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.1); }
    .recipe-image img { width: 100%; height: 180px; object-fit: cover; }
    .no-image { width: 100%; height: 180px; background: #f0f0f0; display: flex; align-items: center; justify-content: center; color: #999; }
    .recipe-info { padding: 1rem; }
    .recipe-info h3 { margin: 0 0 0.5rem; }
    .description { color: #666; font-size: 0.9rem; margin: 0 0 0.5rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .meta { font-size: 0.85rem; color: #888; display: flex; gap: 1rem; }
    .loading, .error, .empty-state { padding: 2rem; text-align: center; }
    .error { background: #f8d7da; color: #721c24; border-radius: 4px; }
    @media (max-width: 600px) { .recipe-grid { grid-template-columns: 1fr; } }
  `]
})
export class HomeComponent implements OnInit {
  recommended: Recipe[] = [];
  loading = false;
  error = '';
  isOwner = false;

  constructor(
    private recipeService: RecipeService,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.authService.user$.subscribe(user => {
      this.isOwner = user?.role === 'Owner';
    });
    this.loadRecommendations();
  }

  loadRecommendations() {
    this.loading = true;
    this.recipeService.getRecommended(8).subscribe({
      next: (data) => {
        this.recommended = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to load recommendations';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
