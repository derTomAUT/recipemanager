import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { IngredientInput, ImportedImageInput, PaperCardParseResponse, StepInput } from '../../models/recipe.model';
import { PaperCardImportService } from '../../services/paper-card-import.service';
import { resolveImageUrl } from '../../utils/url.utils';
import { getApiErrorMessage } from '../household-settings/household-settings.utils';

@Component({
  selector: 'app-paper-card-import',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="paper-import-page">
      <header class="page-header">
        <h1>Import from Paper Card</h1>
        <a routerLink="/home" class="btn btn-secondary">Back</a>
      </header>

      <div *ngIf="error" class="error">{{ error }}</div>
      <div *ngIf="message" class="message">{{ message }}</div>

      <section class="card">
        <h2>1. Add Card Photos</h2>
        <p class="help">Take or upload two photos: front (title/hero) and back (ingredients/steps).</p>
        <div class="file-grid">
          <label class="file-box">
            <span>Front Side</span>
            <input type="file" accept="image/*" capture="environment" (change)="onFrontSelected($event)" />
            <small *ngIf="frontImage">{{ frontImage.name }}</small>
          </label>
          <label class="file-box">
            <span>Back Side</span>
            <input type="file" accept="image/*" capture="environment" (change)="onBackSelected($event)" />
            <small *ngIf="backImage">{{ backImage.name }}</small>
          </label>
        </div>
        <button class="btn btn-primary" [disabled]="!canParse || parsing" (click)="parse()">
          {{ parsing ? 'Parsing...' : 'Parse Paper Card' }}
        </button>
      </section>

      <section class="card" *ngIf="parsed">
        <h2>2. Review & Select Serving Scale</h2>
        <label class="field">
          <span>Recipe Title</span>
          <input [(ngModel)]="title" />
        </label>
        <label class="field">
          <span>Description</span>
          <textarea rows="2" [(ngModel)]="description"></textarea>
        </label>

        <label class="field">
          <span>Store ingredient list for servings</span>
          <select [(ngModel)]="selectedServings" (ngModelChange)="onServingChange()">
            <option [ngValue]="null">Choose serving scale</option>
            <option *ngFor="let s of parsed.servingsAvailable" [ngValue]="s">{{ s }} people</option>
          </select>
        </label>

        <div class="images" *ngIf="importedImages.length">
          <div class="img-item" *ngFor="let img of importedImages; let i = index">
            <img [src]="resolveImage(img.url)" [alt]="i === 0 ? 'Hero image' : 'Step image'" />
            <small>{{ i === 0 ? 'Hero' : 'Step image' }}</small>
          </div>
        </div>

        <div class="sub-section">
          <h3>Ingredients</h3>
          <div *ngFor="let ingredient of ingredients; let i = index" class="row">
            <input [(ngModel)]="ingredient.quantity" [name]="'qty'+i" placeholder="Qty" />
            <input [(ngModel)]="ingredient.unit" [name]="'unit'+i" placeholder="Unit" />
            <input [(ngModel)]="ingredient.name" [name]="'name'+i" placeholder="Ingredient" />
            <input [(ngModel)]="ingredient.notes" [name]="'notes'+i" placeholder="Notes" />
          </div>
          <button class="btn btn-secondary" type="button" (click)="addIngredient()">Add Ingredient</button>
        </div>

        <div class="sub-section">
          <h3>Steps</h3>
          <div *ngFor="let step of steps; let i = index" class="step-row">
            <textarea rows="2" [(ngModel)]="step.instruction" [name]="'step'+i" placeholder="Instruction"></textarea>
          </div>
          <button class="btn btn-secondary" type="button" (click)="addStep()">Add Step</button>
        </div>

        <ul class="warnings" *ngIf="parsed.warnings.length">
          <li *ngFor="let warning of parsed.warnings">{{ warning }}</li>
        </ul>

        <button class="btn btn-primary" [disabled]="saving || !selectedServings" (click)="commit()">
          {{ saving ? 'Saving...' : 'Save Recipe' }}
        </button>
      </section>
    </div>
  `,
  styles: [`
    .paper-import-page { max-width: 860px; margin: 0 auto; padding: 1rem; }
    .page-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; margin-bottom: 1rem; }
    .card { background: var(--surface); border-radius: var(--radius-md); box-shadow: var(--shadow-soft); border: 1px solid color-mix(in srgb, var(--text) 12%, transparent); padding: 1rem; margin-bottom: 1rem; }
    .help { color: var(--muted); margin-top: 0; }
    .file-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-bottom: 1rem; }
    .file-box { display: flex; flex-direction: column; gap: 0.5rem; background: var(--surface-2); border-radius: var(--radius-sm); padding: 0.75rem; }
    .field { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 0.75rem; }
    .images { display: flex; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 0.75rem; }
    .img-item { width: 150px; display: flex; flex-direction: column; gap: 0.25rem; }
    .img-item img { width: 100%; height: 100px; object-fit: cover; border-radius: var(--radius-sm); }
    .sub-section { margin-bottom: 1rem; }
    .row { display: grid; grid-template-columns: 90px 90px 1fr 1fr; gap: 0.5rem; margin-bottom: 0.5rem; }
    .step-row { margin-bottom: 0.5rem; }
    .step-row textarea { width: 100%; }
    .warnings { background: color-mix(in srgb, var(--accent) 22%, var(--surface)); border-radius: var(--radius-sm); padding: 0.75rem 1rem; }
    .btn { border: none; border-radius: var(--radius-sm); padding: 0.65rem 1rem; cursor: pointer; }
    .btn-primary { background: var(--primary); color: white; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }
    .btn-secondary { background: var(--surface-2); color: var(--text); text-decoration: none; display: inline-block; }
    .error { background: color-mix(in srgb, var(--primary) 20%, var(--surface)); color: var(--text); padding: 0.75rem; border-radius: var(--radius-sm); margin-bottom: 1rem; }
    .message { background: color-mix(in srgb, var(--secondary) 20%, var(--surface)); color: var(--text); padding: 0.75rem; border-radius: var(--radius-sm); margin-bottom: 1rem; }
    @media (max-width: 720px) {
      .file-grid { grid-template-columns: 1fr; }
      .row { grid-template-columns: 1fr 1fr; }
    }
  `]
})
export class PaperCardImportComponent {
  frontImage: File | null = null;
  backImage: File | null = null;
  parsing = false;
  saving = false;
  error = '';
  message = '';

  parsed: PaperCardParseResponse | null = null;
  title = '';
  description = '';
  selectedServings: number | null = null;
  importedImages: ImportedImageInput[] = [];
  ingredientsByServings: Record<number, IngredientInput[]> = {};
  ingredients: IngredientInput[] = [];
  steps: StepInput[] = [];

  constructor(
    private paperCardImportService: PaperCardImportService,
    private router: Router
  ) {}

  get canParse(): boolean {
    return !!this.frontImage && !!this.backImage;
  }

  onFrontSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.frontImage = input.files?.[0] ?? null;
  }

  onBackSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.backImage = input.files?.[0] ?? null;
  }

  parse() {
    if (!this.frontImage || !this.backImage) return;
    this.parsing = true;
    this.error = '';
    this.message = '';

    this.paperCardImportService.parse(this.frontImage, this.backImage).subscribe({
      next: (response) => {
        this.parsing = false;
        this.parsed = response;
        this.title = response.title;
        this.description = response.description ?? '';
        this.importedImages = response.importedImages;
        this.ingredientsByServings = response.ingredientsByServings ?? {};
        this.selectedServings = null;
        this.ingredients = [];
        this.steps = (response.steps ?? []).map(step => ({ ...step }));
        this.message = 'Paper card parsed. Choose a serving scale before saving.';
      },
      error: (error) => {
        this.parsing = false;
        this.error = getApiErrorMessage(error, 'Failed to parse paper card photos.');
      }
    });
  }

  onServingChange() {
    if (!this.selectedServings) {
      this.ingredients = [];
      return;
    }

    const selected = this.ingredientsByServings[this.selectedServings] ?? [];
    this.ingredients = selected.map(i => ({ ...i }));
  }

  addIngredient() {
    this.ingredients.push({ name: '', quantity: '', unit: '', notes: '' });
  }

  addStep() {
    this.steps.push({ instruction: '', timerSeconds: undefined });
  }

  commit() {
    if (!this.parsed || !this.selectedServings) {
      this.error = 'Please choose a serving scale first.';
      return;
    }

    this.saving = true;
    this.error = '';

    this.paperCardImportService.commit({
      draftId: this.parsed.draftId,
      selectedServings: this.selectedServings,
      title: this.title.trim(),
      description: this.description.trim() || undefined,
      ingredients: this.ingredients,
      steps: this.steps,
      tags: ['hellofresh', 'paper-card']
    }).subscribe({
      next: ({ recipeId }) => {
        this.saving = false;
        this.router.navigate(['/recipes', recipeId]);
      },
      error: (error) => {
        this.saving = false;
        this.error = getApiErrorMessage(error, 'Failed to save imported paper card recipe.');
      }
    });
  }

  resolveImage(url: string): string {
    return resolveImageUrl(url) ?? url;
  }
}
