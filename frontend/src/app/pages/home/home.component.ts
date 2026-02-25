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
      <section class="hero">
        <div class="hero-copy">
          <p class="eyebrow">Culinary Studio</p>
          <h1>Cook with intention, save with flavor.</h1>
          <p class="hero-subtitle">Import recipes, curate your favorites, and let the kitchen feel like home.</p>
          <div class="hero-actions">
            <a routerLink="/recipes/new" class="btn btn-primary">Import Recipe</a>
            <a routerLink="/recipes" class="btn btn-secondary">Browse All</a>
          </div>
        </div>
        <div class="hero-panel">
          <div class="hero-card">
            <h3>Quick Links</h3>
            <div class="nav-links">
              <a routerLink="/recipes" class="chip">All Recipes</a>
              <a routerLink="/preferences" class="chip">Preferences</a>
              <a routerLink="/logs" class="chip">Logs</a>
              <a *ngIf="isOwner" routerLink="/household/settings" class="chip">Household Settings</a>
            </div>
          </div>
          <div class="hero-card accent">
            <p class="accent-label">Tonight's vibe</p>
            <h3>Warm, quick, and shareable</h3>
            <p>Pick a dish in under 20 minutes with smart recommendations.</p>
          </div>
        </div>
      </section>

      <section class="recommended-section">
        <div class="section-header">
          <h2>Recommended For You</h2>
          <span class="chip">Fresh picks</span>
        </div>
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
    .home-page { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
    .hero { display: grid; grid-template-columns: 1.2fr 0.8fr; gap: 2rem; margin-bottom: 2.5rem; }
    .hero-copy { padding: 1.5rem; border-radius: var(--radius-lg); background: radial-gradient(circle at top left, rgba(231,184,75,0.4), transparent 55%), var(--surface); box-shadow: var(--shadow); }
    .hero-copy h1 { margin: 0.5rem 0 0.75rem; font-size: clamp(2rem, 3vw, 3rem); }
    .hero-subtitle { color: var(--muted); margin-bottom: 1.5rem; }
    .hero-actions { display: flex; gap: 0.75rem; flex-wrap: wrap; }
    .hero-panel { display: grid; gap: 1rem; }
    .hero-card { background: var(--surface); border-radius: var(--radius-lg); padding: 1.25rem; box-shadow: var(--shadow-soft); }
    .hero-card.accent { background: linear-gradient(135deg, rgba(217,80,47,0.15), rgba(110,159,122,0.15)), var(--surface); }
    .accent-label { text-transform: uppercase; font-size: 0.75rem; letter-spacing: 0.12em; color: var(--muted); }
    .eyebrow { text-transform: uppercase; letter-spacing: 0.2em; font-size: 0.7rem; color: var(--muted); margin: 0; }
    .nav-links { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 0.75rem; }

    .section-header { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 1rem; }
    .recipe-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 1rem; }
    .recipe-card { border-radius: var(--radius-lg); overflow: hidden; cursor: pointer; background: var(--surface); box-shadow: var(--shadow-soft); transition: transform 0.2s ease, box-shadow 0.2s ease; }
    .recipe-card:hover { transform: translateY(-4px); box-shadow: var(--shadow); }
    .recipe-image img { width: 100%; height: 180px; object-fit: cover; }
    .no-image { width: 100%; height: 180px; background: var(--surface-2); display: flex; align-items: center; justify-content: center; color: var(--muted); }
    .recipe-info { padding: 1rem; }
    .recipe-info h3 { margin: 0 0 0.4rem; }
    .description { color: var(--muted); font-size: 0.9rem; margin: 0 0 0.5rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .meta { font-size: 0.85rem; color: var(--muted); display: flex; gap: 1rem; }
    .loading, .error, .empty-state { padding: 2rem; text-align: center; }
    .error { background: rgba(217,80,47,0.15); color: var(--text); border-radius: var(--radius-md); }

    @media (max-width: 900px) {
      .hero { grid-template-columns: 1fr; }
    }
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
