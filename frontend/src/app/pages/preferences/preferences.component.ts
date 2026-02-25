import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PreferencesService } from '../../services/preferences.service';
import { UserPreferences } from '../../models/preference.model';

@Component({
  selector: 'app-preferences',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="preferences-page">
      <header class="page-header">
        <h1>My Preferences</h1>
        <a routerLink="/recipes" class="btn btn-secondary">Back</a>
      </header>

      <div *ngIf="loading" class="loading">Loading preferences...</div>
      <div *ngIf="error" class="error">{{ error }}</div>
      <div *ngIf="success" class="success">Preferences saved!</div>

      <form *ngIf="!loading && preferences" (ngSubmit)="save()">
        <section class="pref-section allergens">
          <h2>Allergens</h2>
          <p class="hint">Recipes containing these will be hidden from recommendations.</p>
          <div class="chips">
            <span *ngFor="let item of preferences.allergens; let i = index" class="chip chip-red">
              {{ item }}
              <button type="button" (click)="removeItem('allergens', i)">x</button>
            </span>
          </div>
          <div class="add-input">
            <input [(ngModel)]="newAllergen" name="newAllergen" placeholder="Add allergen..." (keyup.enter)="addItem('allergens', newAllergen); newAllergen = ''" />
            <button type="button" (click)="addItem('allergens', newAllergen); newAllergen = ''">Add</button>
          </div>
        </section>

        <section class="pref-section dislikes">
          <h2>Disliked Ingredients</h2>
          <p class="hint">Recipes with these will be ranked lower.</p>
          <div class="chips">
            <span *ngFor="let item of preferences.dislikedIngredients; let i = index" class="chip chip-yellow">
              {{ item }}
              <button type="button" (click)="removeItem('dislikedIngredients', i)">x</button>
            </span>
          </div>
          <div class="add-input">
            <input [(ngModel)]="newDislike" name="newDislike" placeholder="Add disliked ingredient..." (keyup.enter)="addItem('dislikedIngredients', newDislike); newDislike = ''" />
            <button type="button" (click)="addItem('dislikedIngredients', newDislike); newDislike = ''">Add</button>
          </div>
        </section>

        <section class="pref-section cuisines">
          <h2>Favorite Cuisines</h2>
          <p class="hint">Recipes with these cuisines will be ranked higher.</p>
          <div class="chips">
            <span *ngFor="let item of preferences.favoriteCuisines; let i = index" class="chip chip-blue">
              {{ item }}
              <button type="button" (click)="removeItem('favoriteCuisines', i)">x</button>
            </span>
          </div>
          <div class="add-input">
            <input [(ngModel)]="newCuisine" name="newCuisine" placeholder="Add favorite cuisine..." (keyup.enter)="addItem('favoriteCuisines', newCuisine); newCuisine = ''" />
            <button type="button" (click)="addItem('favoriteCuisines', newCuisine); newCuisine = ''">Add</button>
          </div>
        </section>

        <div class="form-actions">
          <button type="submit" class="btn btn-primary" [disabled]="saving">
            {{ saving ? 'Saving...' : 'Save Preferences' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .preferences-page { padding: 1rem; max-width: 600px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    .page-header h1 { margin: 0; }
    .btn { padding: 0.75rem 1rem; min-height: 44px; border: none; border-radius: 4px; cursor: pointer; text-decoration: none; }
    .btn-primary { background: #007bff; color: white; }
    .btn-primary:disabled { opacity: 0.6; }
    .btn-secondary { background: #6c757d; color: white; }
    .pref-section { margin-bottom: 2rem; padding: 1rem; border: 1px solid #eee; border-radius: 8px; }
    .pref-section h2 { margin: 0 0 0.5rem; font-size: 1.1rem; }
    .hint { color: #666; font-size: 0.875rem; margin: 0 0 1rem; }
    .chips { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; min-height: 32px; }
    .chip { display: inline-flex; align-items: center; gap: 0.25rem; padding: 0.25rem 0.5rem; border-radius: 16px; font-size: 0.875rem; }
    .chip button { background: none; border: none; cursor: pointer; font-size: 1.25rem; padding: 0.25rem; min-width: 32px; min-height: 32px; display: inline-flex; align-items: center; justify-content: center; opacity: 0.7; }
    .chip button:hover { opacity: 1; }
    .chip-red { background: #f8d7da; color: #721c24; }
    .chip-yellow { background: #fff3cd; color: #856404; }
    .chip-blue { background: #cce5ff; color: #004085; }
    .add-input { display: flex; gap: 0.5rem; }
    .add-input input { flex: 1; padding: 0.5rem; min-height: 44px; border: 1px solid #ddd; border-radius: 4px; }
    .add-input button { padding: 0.5rem 1rem; min-height: 44px; background: #28a745; color: white; border: none; border-radius: 4px; cursor: pointer; }
    .form-actions { margin-top: 1.5rem; }
    .loading, .error, .success { padding: 1rem; border-radius: 4px; margin-bottom: 1rem; text-align: center; }
    .loading { background: #f0f0f0; }
    .error { background: #f8d7da; color: #721c24; }
    .success { background: #d4edda; color: #155724; }
    @media (max-width: 600px) {
      .add-input { flex-direction: column; }
      .add-input button { width: 100%; }
    }
  `]
})
export class PreferencesComponent implements OnInit {
  preferences: UserPreferences | null = null;
  loading = false;
  saving = false;
  error = '';
  success = false;

  newAllergen = '';
  newDislike = '';
  newCuisine = '';

  constructor(private preferencesService: PreferencesService) {}

  ngOnInit() {
    this.loadPreferences();
  }

  loadPreferences() {
    this.loading = true;
    this.preferencesService.getPreferences().subscribe({
      next: (data) => {
        this.preferences = data;
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load preferences';
        this.loading = false;
      }
    });
  }

  addItem(field: 'allergens' | 'dislikedIngredients' | 'favoriteCuisines', value: string) {
    if (!this.preferences || !value.trim()) return;
    const trimmed = value.trim().toLowerCase();
    if (!this.preferences[field].includes(trimmed)) {
      this.preferences[field].push(trimmed);
    }
  }

  removeItem(field: 'allergens' | 'dislikedIngredients' | 'favoriteCuisines', index: number) {
    if (!this.preferences) return;
    this.preferences[field].splice(index, 1);
  }

  save() {
    if (!this.preferences) return;
    this.saving = true;
    this.error = '';
    this.success = false;

    this.preferencesService.updatePreferences(this.preferences).subscribe({
      next: (data) => {
        this.preferences = data;
        this.saving = false;
        this.success = true;
        setTimeout(() => this.success = false, 3000);
      },
      error: () => {
        this.error = 'Failed to save preferences';
        this.saving = false;
      }
    });
  }
}
