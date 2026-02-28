import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MealAssistantResponse } from '../../models/recipe.model';
import { RecipeService } from '../../services/recipe.service';

@Component({
  selector: 'app-meal-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="meal-assistant-page">
      <header class="page-header">
        <h1>Meal Assistant</h1>
        <a routerLink="/home" class="btn btn-secondary">Back</a>
      </header>

      <section class="card">
        <p class="help">Ask for meal ideas using your recipes, preferences, allergens, and seasonal context.</p>
        <label class="field">
          <span>Prompt</span>
          <textarea
            rows="3"
            [(ngModel)]="prompt"
            placeholder="Something quick and cozy for tonight..."
          ></textarea>
        </label>
        <div class="actions">
          <button class="btn btn-primary" type="button" (click)="suggest()" [disabled]="loading || !prompt.trim()">
            {{ loading ? 'Thinking...' : 'Suggest Meals' }}
          </button>
        </div>
      </section>

      <div *ngIf="error" class="error">{{ error }}</div>

      <section *ngIf="result" class="card">
        <div class="context">
          <span><strong>Season:</strong> {{ result.season }}</span>
          <span><strong>Hemisphere:</strong> {{ result.hemisphere }}</span>
          <span><strong>Month:</strong> {{ result.month }}</span>
          <span><strong>Source:</strong> {{ result.usedAi ? 'AI ranking' : 'Deterministic fallback' }}</span>
        </div>

        <ul class="warnings" *ngIf="result.warnings.length">
          <li *ngFor="let warning of result.warnings">{{ warning }}</li>
        </ul>

        <div class="suggestions">
          <article class="suggestion" *ngFor="let s of result.suggestions; let i = index">
            <div class="rank">{{ i + 1 }}</div>
            <div class="body">
              <a [routerLink]="['/recipes', s.recipeId]" class="title">{{ s.title }}</a>
              <p class="reason">{{ s.reason }}</p>
              <p class="warning" *ngIf="s.warning">{{ s.warning }}</p>
            </div>
            <img *ngIf="s.titleImageUrl" [src]="s.titleImageUrl" [alt]="s.title" />
          </article>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .meal-assistant-page { max-width: 900px; margin: 0 auto; padding: 1rem; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; gap: 0.7rem; }
    .card { background: var(--surface); border-radius: var(--radius-md); border: 1px solid color-mix(in srgb, var(--text) 12%, transparent); box-shadow: var(--shadow-soft); padding: 1rem; margin-bottom: 1rem; }
    .help { margin-top: 0; color: var(--muted); }
    .field { display: flex; flex-direction: column; gap: 0.35rem; }
    textarea { width: 100%; border-radius: var(--radius-sm); border: 1px solid color-mix(in srgb, var(--text) 20%, transparent); background: var(--surface-2); color: var(--text); padding: 0.6rem; }
    .actions { margin-top: 0.7rem; }
    .btn { border: none; border-radius: var(--radius-sm); padding: 0.6rem 1rem; cursor: pointer; text-decoration: none; display: inline-block; }
    .btn-primary { background: var(--primary); color: #fff; }
    .btn-secondary { background: var(--surface-2); color: var(--text); }
    .error { background: color-mix(in srgb, var(--primary) 20%, var(--surface)); color: var(--text); padding: 0.75rem; border-radius: var(--radius-sm); margin-bottom: 1rem; }
    .context { display: flex; gap: 0.9rem; flex-wrap: wrap; color: var(--muted); font-size: 0.9rem; margin-bottom: 0.7rem; }
    .warnings { margin: 0 0 0.7rem; padding-left: 1.1rem; color: var(--muted); }
    .suggestions { display: grid; gap: 0.6rem; }
    .suggestion { display: grid; grid-template-columns: 32px 1fr 120px; gap: 0.7rem; align-items: start; padding: 0.6rem; border: 1px solid color-mix(in srgb, var(--text) 12%, transparent); border-radius: var(--radius-sm); background: var(--surface-2); }
    .rank { width: 32px; height: 32px; border-radius: 50%; background: var(--primary); color: #fff; display: flex; justify-content: center; align-items: center; font-weight: 700; }
    .title { color: var(--text); font-weight: 700; text-decoration: none; }
    .reason { margin: 0.2rem 0 0; color: var(--text); }
    .warning { margin: 0.3rem 0 0; color: var(--muted); font-size: 0.9rem; }
    img { width: 120px; height: 90px; object-fit: cover; border-radius: var(--radius-sm); }
    @media (max-width: 640px) {
      .suggestion { grid-template-columns: 28px 1fr; }
      img { width: 100%; height: 140px; grid-column: 1 / -1; }
    }
  `]
})
export class MealAssistantComponent {
  prompt = '';
  loading = false;
  error = '';
  result: MealAssistantResponse | null = null;

  constructor(private recipeService: RecipeService) {}

  suggest() {
    const trimmed = this.prompt.trim();
    if (!trimmed) return;
    this.loading = true;
    this.error = '';
    this.recipeService.getMealAssistantSuggestions(trimmed).subscribe({
      next: (result) => {
        this.result = result;
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to get meal suggestions.';
        this.loading = false;
      }
    });
  }
}
