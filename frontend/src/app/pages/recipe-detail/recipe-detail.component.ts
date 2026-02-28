import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { RecipeService } from '../../services/recipe.service';
import { RecipeDetail } from '../../models/recipe.model';

@Component({
  selector: 'app-recipe-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="recipe-detail-page" *ngIf="recipe">
      <header class="sticky-header">
        <div class="header-title">
          <h1>{{ recipe.title }}</h1>
          <div *ngIf="recipe.sourceUrl" class="source-url">{{ recipe.sourceUrl }}</div>
        </div>
        <div class="header-actions">
          <button (click)="markCooked()" [disabled]="marking" class="btn btn-primary">
            {{ marking ? 'Marking...' : 'Mark as Cooked' }}
          </button>
          <a [routerLink]="['/recipes', recipe.id, 'edit']" class="btn btn-secondary">Edit</a>
          <button (click)="confirmDelete()" class="btn btn-danger">Delete</button>
        </div>
      </header>

      <div class="recipe-content">
        <div *ngIf="successMessage" class="success">{{ successMessage }}</div>

        <section class="recipe-meta">
          <div *ngIf="recipe.description" class="description">{{ recipe.description }}</div>
          <div class="meta-row">
            <span *ngIf="recipe.prepMinutes"><strong>Prep:</strong> {{ recipe.prepMinutes }} min</span>
            <span *ngIf="recipe.cookMinutes"><strong>Cook:</strong> {{ recipe.cookMinutes }} min</span>
            <span *ngIf="recipe.servings"><strong>Servings:</strong> {{ recipe.servings }}</span>
            <span *ngIf="recipe.cookCount"><strong>Cooked:</strong> {{ recipe.cookCount }}x</span>
          </div>
          <div class="tags" *ngIf="recipe.tags.length">
            <span *ngFor="let tag of recipe.tags" class="tag">{{ tag }}</span>
          </div>
        </section>

        <section class="images" *ngIf="heroImage">
          <div class="hero-image">
            <img [src]="heroImage.url" [alt]="recipe.title" />
          </div>
        </section>

        <section class="ingredients">
          <h2>Ingredients</h2>
          <ul class="ingredient-list">
            <li *ngFor="let ing of recipe.ingredients">
              <span class="quantity">{{ ing.quantity }} {{ ing.unit }}</span>
              <span class="name">{{ ing.name }}</span>
              <span *ngIf="ing.notes" class="notes">({{ ing.notes }})</span>
            </li>
          </ul>
        </section>

        <section class="steps">
          <h2>Instructions</h2>
          <div class="steps-layout">
            <ol class="step-list">
              <li *ngFor="let step of recipe.steps; let i = index" class="step">
                <div class="step-number">{{ i + 1 }}</div>
                <div class="step-content">
                  <p>{{ step.instruction }}</p>
                  <span *ngIf="step.timerSeconds" class="timer">Timer: {{ formatTime(step.timerSeconds) }}</span>
                </div>
              </li>
            </ol>
            <div class="step-rail" *ngIf="stepImages.length">
              <div class="step-rail-item" *ngFor="let img of stepImages">
                <img [src]="img.url" [alt]="recipe.title + ' step image'" />
              </div>
            </div>
          </div>
        </section>
      </div>

      <a routerLink="/recipes" class="back-link">Back to Recipes</a>
    </div>

    <div *ngIf="loading" class="loading">Loading recipe...</div>
    <div *ngIf="error" class="error">{{ error }}</div>
    <div *ngIf="!recipe && !loading && !error" class="not-found">
      <p>Recipe not found.</p>
      <a routerLink="/recipes">Back to Recipes</a>
    </div>
  `,
  styles: [`
    .recipe-detail-page { padding: 1rem; max-width: 800px; margin: 0 auto; }
    .sticky-header { position: sticky; top: 0; background: var(--bg); padding: 1rem 0; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(0,0,0,0.1); z-index: 10; }
    .header-title { display: flex; flex-direction: column; gap: 0.25rem; }
    .sticky-header h1 { margin: 0; font-size: 1.5rem; }
    .header-actions { display: flex; gap: 0.5rem; }
    .btn-danger { background: #d23f3f; color: white; }
    .btn-danger:disabled { opacity: 0.6; cursor: not-allowed; }
    .recipe-meta { margin: 1rem 0; }
    .description { color: var(--muted); margin-bottom: 1rem; }
    .meta-row { display: flex; flex-wrap: wrap; gap: 1rem; font-size: 0.9rem; color: var(--muted); }
    .tags { margin-top: 0.5rem; display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .tag { background: var(--surface-2); padding: 0.25rem 0.5rem; border-radius: 999px; font-size: 0.85rem; color: var(--muted); }
    .images { margin: 1rem 0; }
    .hero-image img { width: 100%; max-height: 380px; object-fit: cover; border-radius: 12px; }
    .ingredients, .steps { margin: 1.5rem 0; }
    .ingredients h2, .steps h2 { font-size: 1.25rem; margin-bottom: 1rem; }
    .ingredient-list { list-style: none; padding: 0; }
    .ingredient-list li { padding: 0.5rem 0; border-bottom: 1px solid rgba(0,0,0,0.08); display: flex; gap: 0.5rem; }
    .quantity { font-weight: 500; min-width: 80px; }
    .notes { color: var(--muted); font-size: 0.9rem; }
    .steps-layout { display: grid; grid-template-columns: minmax(0, 1fr) minmax(180px, 260px); gap: 1.5rem; align-items: stretch; }
    .step-list { list-style: none; padding: 0; counter-reset: step; margin: 0; }
    .step { display: grid; grid-template-columns: 32px 1fr; gap: 1rem; padding: 1rem 0; border-bottom: 1px solid rgba(0,0,0,0.08); }
    .step-number { width: 32px; height: 32px; border-radius: 50%; background: var(--primary); color: white; display: flex; align-items: center; justify-content: center; font-weight: bold; flex-shrink: 0; }
    .step-content { flex: 1; }
    .step-rail { display: flex; flex-direction: column; justify-content: space-between; gap: 0.75rem; }
    .step-rail-item img { width: 100%; height: 140px; object-fit: cover; border-radius: 12px; }
    .step-content p { margin: 0; font-size: 1.1rem; line-height: 1.6; }
    .timer { color: var(--primary); font-size: 0.9rem; margin-top: 0.5rem; display: inline-block; }
    .back-link { display: inline-block; margin-top: 2rem; color: var(--primary); }
    .loading { text-align: center; padding: 2rem; }
    .error { color: var(--text); text-align: center; padding: 1rem; background: rgba(217,80,47,0.15); border-radius: 4px; }
    .success { color: var(--text); margin: 0.5rem 0 1rem; padding: 0.8rem 1rem; background: color-mix(in srgb, var(--secondary) 22%, var(--surface)); border-radius: var(--radius-sm); }
    .not-found { text-align: center; padding: 2rem; color: var(--muted); }
    .not-found a { color: var(--primary); }
    @media (max-width: 900px) {
      .steps-layout { grid-template-columns: 1fr; }
      .step-rail { flex-direction: row; overflow-x: auto; scroll-snap-type: x mandatory; padding-bottom: 0.5rem; }
      .step-rail-item { scroll-snap-align: start; flex: 0 0 auto; }
      .step-rail-item img { width: 240px; height: 150px; }
    }
    @media (max-width: 600px) {
      .sticky-header { flex-direction: column; gap: 0.5rem; align-items: flex-start; }
      .step-content p { font-size: 1rem; }
    }
  `]
})
export class RecipeDetailComponent implements OnInit {
  recipe: RecipeDetail | null = null;
  loading = false;
  error = '';
  marking = false;
  successMessage = '';
  heroImage: { url: string } | null = null;
  stepImages: { url: string }[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private recipeService: RecipeService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadRecipe(id);
    } else {
      this.error = 'No recipe ID provided';
    }
  }

  loadRecipe(id: string) {
    this.loading = true;
    this.error = '';
    this.recipeService.getRecipe(id).subscribe({
      next: (recipe) => {
        this.recipe = recipe;
        this.heroImage = this.recipe.images.find(img => img.isTitleImage)
          ?? this.recipe.images.find(img => img.orderIndex === 0)
          ?? null;
        this.stepImages = this.recipe.images
          .filter(img => !img.isTitleImage && img.orderIndex !== 0);
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        if (err.status === 404) {
          this.error = 'Recipe not found';
        } else if (err.status === 401) {
          this.error = 'Please log in again';
        } else {
          this.error = 'Failed to load recipe. Please try again.';
        }
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  confirmDelete() {
    if (!this.recipe) return;
    if (confirm('Are you sure you want to delete this recipe?')) {
      this.recipeService.deleteRecipe(this.recipe.id).subscribe({
        next: () => this.router.navigate(['/recipes']),
        error: () => this.error = 'Failed to delete recipe'
      });
    }
  }

  formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return mins > 0 ? `${mins}m ${secs}s` : `${secs}s`;
  }

  markCooked() {
    if (!this.recipe) return;
    this.marking = true;
    this.successMessage = '';
    this.recipeService.markCooked(this.recipe.id).subscribe({
      next: () => {
        this.marking = false;
        this.recipe!.cookCount++;
        this.successMessage = 'Recipe marked as cooked.';
      },
      error: () => {
        this.marking = false;
        this.error = 'Failed to mark as cooked';
      }
    });
  }
}
