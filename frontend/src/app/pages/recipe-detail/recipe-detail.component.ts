import { Component, OnInit } from '@angular/core';
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
        <h1>{{ recipe.title }}</h1>
        <div class="header-actions">
          <button (click)="markCooked()" [disabled]="marking" class="btn btn-success">
            {{ marking ? 'Marking...' : 'Mark as Cooked' }}
          </button>
          <a [routerLink]="['/recipes', recipe.id, 'edit']" class="btn">Edit</a>
          <button (click)="confirmDelete()" class="btn btn-danger">Delete</button>
        </div>
      </header>

      <div class="recipe-content">
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

        <section class="images" *ngIf="recipe.images.length">
          <div class="image-gallery">
            <img *ngFor="let img of recipe.images" [src]="img.url" [alt]="recipe.title" />
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
          <ol class="step-list">
            <li *ngFor="let step of recipe.steps; let i = index" class="step">
              <div class="step-number">{{ i + 1 }}</div>
              <div class="step-content">
                <p>{{ step.instruction }}</p>
                <span *ngIf="step.timerSeconds" class="timer">Timer: {{ formatTime(step.timerSeconds) }}</span>
              </div>
            </li>
          </ol>
        </section>
      </div>

      <a routerLink="/recipes" class="back-link">Back to Recipes</a>
    </div>

    <div *ngIf="loading" class="loading">Loading recipe...</div>
    <div *ngIf="error" class="error">{{ error }}</div>
  `,
  styles: [`
    .recipe-detail-page { padding: 1rem; max-width: 800px; margin: 0 auto; }
    .sticky-header { position: sticky; top: 0; background: white; padding: 1rem 0; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #ddd; z-index: 10; }
    .sticky-header h1 { margin: 0; font-size: 1.5rem; }
    .header-actions { display: flex; gap: 0.5rem; }
    .btn { padding: 0.75rem 1rem; min-height: 44px; text-decoration: none; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; }
    .btn-danger { background: #dc3545; color: white; border-color: #dc3545; }
    .btn-success { background: #28a745; color: white; border-color: #28a745; }
    .btn-success:disabled { background: #6c757d; border-color: #6c757d; cursor: not-allowed; }
    .recipe-meta { margin: 1rem 0; }
    .description { color: #666; margin-bottom: 1rem; }
    .meta-row { display: flex; flex-wrap: wrap; gap: 1rem; font-size: 0.9rem; color: #666; }
    .tags { margin-top: 0.5rem; display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .tag { background: #e0e0e0; padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.85rem; }
    .images { margin: 1rem 0; }
    .image-gallery { display: flex; gap: 0.5rem; overflow-x: auto; }
    .image-gallery img { max-height: 300px; border-radius: 8px; }
    .ingredients, .steps { margin: 1.5rem 0; }
    .ingredients h2, .steps h2 { font-size: 1.25rem; margin-bottom: 1rem; }
    .ingredient-list { list-style: none; padding: 0; }
    .ingredient-list li { padding: 0.5rem 0; border-bottom: 1px solid #eee; display: flex; gap: 0.5rem; }
    .quantity { font-weight: 500; min-width: 80px; }
    .notes { color: #888; font-size: 0.9rem; }
    .step-list { list-style: none; padding: 0; counter-reset: step; }
    .step { display: flex; gap: 1rem; padding: 1rem 0; border-bottom: 1px solid #eee; }
    .step-number { width: 32px; height: 32px; border-radius: 50%; background: #007bff; color: white; display: flex; align-items: center; justify-content: center; font-weight: bold; flex-shrink: 0; }
    .step-content { flex: 1; }
    .step-content p { margin: 0; font-size: 1.1rem; line-height: 1.6; }
    .timer { color: #007bff; font-size: 0.9rem; margin-top: 0.5rem; display: inline-block; }
    .back-link { display: inline-block; margin-top: 2rem; color: #007bff; }
    .loading { text-align: center; padding: 2rem; }
    .error { color: #dc3545; text-align: center; padding: 1rem; background: #f8d7da; border-radius: 4px; }
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

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private recipeService: RecipeService
  ) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadRecipe(id);
    }
  }

  loadRecipe(id: string) {
    this.loading = true;
    this.recipeService.getRecipe(id).subscribe({
      next: (recipe) => {
        this.recipe = recipe;
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load recipe';
        this.loading = false;
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
    this.recipeService.markCooked(this.recipe.id).subscribe({
      next: () => {
        this.marking = false;
        this.recipe!.cookCount++;
        alert('Recipe marked as cooked!');
      },
      error: () => {
        this.marking = false;
        this.error = 'Failed to mark as cooked';
      }
    });
  }
}
