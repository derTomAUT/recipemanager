import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { RecipeService } from '../../services/recipe.service';
import { RecipeImage, CreateRecipeRequest, IngredientInput, StepInput } from '../../models/recipe.model';

@Component({
  selector: 'app-recipe-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="editor-page">
      <header class="editor-header">
        <h1>{{ isEdit ? 'Edit Recipe' : 'New Recipe' }}</h1>
        <div class="header-actions">
          <button type="button" (click)="cancel()" class="btn btn-secondary">Cancel</button>
          <button type="button" (click)="save()" [disabled]="saving" class="btn btn-primary">
            {{ saving ? 'Saving...' : 'Save Recipe' }}
          </button>
        </div>
      </header>

      <form class="editor-form" (ngSubmit)="save()">
        <div *ngIf="loading" class="loading">Loading recipe...</div>
        <div *ngIf="error" class="error">{{ error }}</div>

        <section class="form-section">
          <h2>Basic Info</h2>
          <div class="form-group">
            <label for="title">Title *</label>
            <input id="title" type="text" [(ngModel)]="recipe.title" name="title" required maxlength="200" />
          </div>
          <div class="form-group">
            <label for="description">Description</label>
            <textarea id="description" [(ngModel)]="recipe.description" name="description" rows="3"></textarea>
          </div>
          <div class="form-row">
            <div class="form-group">
              <label for="servings">Servings</label>
              <input id="servings" type="number" [(ngModel)]="recipe.servings" name="servings" min="1" />
            </div>
            <div class="form-group">
              <label for="prepMinutes">Prep Time (min)</label>
              <input id="prepMinutes" type="number" [(ngModel)]="recipe.prepMinutes" name="prepMinutes" min="0" />
            </div>
            <div class="form-group">
              <label for="cookMinutes">Cook Time (min)</label>
              <input id="cookMinutes" type="number" [(ngModel)]="recipe.cookMinutes" name="cookMinutes" min="0" />
            </div>
          </div>
          <div class="form-group">
            <label for="tags">Tags (comma-separated)</label>
            <input id="tags" type="text" [(ngModel)]="tagsInput" name="tags" placeholder="e.g., dinner, italian, quick" />
          </div>
        </section>

        <section class="form-section">
          <h2>Ingredients</h2>
          <div class="dynamic-list">
            <div *ngFor="let ing of ingredients; let i = index" class="list-item ingredient-row">
              <input [(ngModel)]="ing.quantity" [name]="'ingQty'+i" placeholder="Qty" class="qty-input" />
              <input [(ngModel)]="ing.unit" [name]="'ingUnit'+i" placeholder="Unit" class="unit-input" />
              <input [(ngModel)]="ing.name" [name]="'ingName'+i" placeholder="Ingredient name *" required class="name-input" />
              <input [(ngModel)]="ing.notes" [name]="'ingNotes'+i" placeholder="Notes" class="notes-input" />
              <button type="button" (click)="removeIngredient(i)" class="btn-remove" aria-label="Remove ingredient">×</button>
            </div>
          </div>
          <button type="button" (click)="addIngredient()" class="btn btn-add">+ Add Ingredient</button>
        </section>

        <section class="form-section">
          <h2>Instructions</h2>
          <div class="dynamic-list">
            <div *ngFor="let step of steps; let i = index" class="list-item step-row">
              <span class="step-number">{{ i + 1 }}</span>
              <textarea [(ngModel)]="step.instruction" [name]="'stepInstr'+i" placeholder="Step instructions *" required rows="2" class="step-input"></textarea>
              <input [(ngModel)]="step.timerSeconds" [name]="'stepTimer'+i" placeholder="Timer (sec)" type="number" min="0" class="timer-input" />
              <button type="button" (click)="removeStep(i)" class="btn-remove" aria-label="Remove step">×</button>
            </div>
          </div>
          <button type="button" (click)="addStep()" class="btn btn-add">+ Add Step</button>
        </section>

        <section class="form-section" *ngIf="isEdit && recipeId">
          <h2>Images</h2>
          <div class="image-grid">
            <div *ngFor="let img of images" class="image-item" [class.title-image]="img.isTitleImage">
              <img [src]="img.url" [alt]="recipe.title" />
              <div class="image-actions">
                <button type="button" *ngIf="!img.isTitleImage" (click)="setAsTitleImage(img.id)" class="btn-small">Set as Title</button>
                <button type="button" (click)="deleteImage(img.id)" class="btn-small btn-danger">Delete</button>
              </div>
              <span *ngIf="img.isTitleImage" class="title-badge">Title Image</span>
            </div>
          </div>
          <div class="upload-area">
            <input type="file" accept="image/*" (change)="onFileSelect($event)" [disabled]="uploading" />
            <span *ngIf="uploading">Uploading...</span>
          </div>
        </section>
      </form>
    </div>
  `,
  styles: [`
    .editor-page { padding: 1rem; max-width: 800px; margin: 0 auto; }
    .editor-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; flex-wrap: wrap; gap: 1rem; }
    .editor-header h1 { margin: 0; }
    .header-actions { display: flex; gap: 0.5rem; }
    .btn { padding: 0.75rem 1rem; min-height: 44px; border: none; border-radius: 4px; cursor: pointer; font-size: 1rem; }
    .btn-primary { background: #007bff; color: white; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }
    .btn-secondary { background: #6c757d; color: white; }
    .btn-add { background: #28a745; color: white; margin-top: 0.5rem; }
    .btn-small { padding: 0.5rem 0.75rem; font-size: 0.85rem; min-height: 44px; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; }
    .btn-danger { background: #dc3545; color: white; border-color: #dc3545; }
    .btn-remove { background: #dc3545; color: white; border: none; border-radius: 4px; width: 44px; height: 44px; min-width: 44px; min-height: 44px; cursor: pointer; font-size: 1.25rem; flex-shrink: 0; }
    .form-section { margin-bottom: 2rem; padding: 1rem; border: 1px solid #eee; border-radius: 8px; }
    .form-section h2 { margin: 0 0 1rem; font-size: 1.1rem; color: #333; }
    .form-group { margin-bottom: 1rem; }
    .form-group label { display: block; margin-bottom: 0.25rem; font-weight: 500; }
    .form-group input, .form-group textarea { width: 100%; padding: 0.5rem; font-size: 1rem; border: 1px solid #ddd; border-radius: 4px; box-sizing: border-box; }
    .form-row { display: flex; gap: 1rem; flex-wrap: wrap; }
    .form-row .form-group { flex: 1; min-width: 120px; }
    .dynamic-list { display: flex; flex-direction: column; gap: 0.5rem; }
    .list-item { display: flex; gap: 0.5rem; align-items: flex-start; }
    .ingredient-row .qty-input { width: 60px; }
    .ingredient-row .unit-input { width: 70px; }
    .ingredient-row .name-input { flex: 1; min-width: 120px; }
    .ingredient-row .notes-input { width: 100px; }
    .step-row { align-items: stretch; }
    .step-number { width: 28px; height: 28px; border-radius: 50%; background: #007bff; color: white; display: flex; align-items: center; justify-content: center; font-weight: bold; flex-shrink: 0; margin-top: 0.25rem; }
    .step-input { flex: 1; min-width: 150px; }
    .timer-input { width: 80px; }
    .image-grid { display: flex; flex-wrap: wrap; gap: 1rem; margin-bottom: 1rem; }
    .image-item { position: relative; width: 150px; }
    .image-item img { width: 100%; height: 100px; object-fit: cover; border-radius: 4px; }
    .image-item.title-image { border: 3px solid #007bff; border-radius: 4px; }
    .image-actions { display: flex; gap: 0.25rem; margin-top: 0.25rem; }
    .title-badge { position: absolute; top: 4px; left: 4px; background: #007bff; color: white; padding: 0.125rem 0.25rem; font-size: 0.7rem; border-radius: 2px; }
    .upload-area { margin-top: 0.5rem; }
    .upload-area input { padding: 0.5rem; }
    .error { color: #dc3545; padding: 0.75rem; background: #f8d7da; border-radius: 4px; margin-bottom: 1rem; }
    .loading { text-align: center; padding: 2rem; color: #666; }
    @media (max-width: 600px) {
      .ingredient-row { flex-wrap: wrap; }
      .ingredient-row .qty-input, .ingredient-row .unit-input { width: calc(50% - 0.25rem); }
      .ingredient-row .name-input, .ingredient-row .notes-input { width: 100%; }
      .step-row { flex-wrap: wrap; }
      .timer-input { width: 100%; }
      .form-row .form-group { min-width: 100%; }
    }
  `]
})
export class RecipeEditorComponent implements OnInit {
  isEdit = false;
  recipeId: string | null = null;
  saving = false;
  uploading = false;
  loading = false;
  error = '';

  recipe: CreateRecipeRequest = {
    title: '',
    description: '',
    servings: undefined,
    prepMinutes: undefined,
    cookMinutes: undefined,
    ingredients: [],
    steps: [],
    tags: []
  };

  ingredients: IngredientInput[] = [];
  steps: StepInput[] = [];
  images: RecipeImage[] = [];
  tagsInput = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private recipeService: RecipeService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.recipeId = this.route.snapshot.paramMap.get('id');
    this.isEdit = !!this.recipeId;

    if (this.isEdit && this.recipeId) {
      this.loading = true;
      this.loadRecipe(this.recipeId);
    } else {
      this.addIngredient();
      this.addStep();
    }
  }

  loadRecipe(id: string) {
    this.recipeService.getRecipe(id).subscribe({
      next: (data) => {
        this.recipe = {
          title: data.title,
          description: data.description || '',
          servings: data.servings,
          prepMinutes: data.prepMinutes,
          cookMinutes: data.cookMinutes,
          ingredients: [],
          steps: [],
          tags: data.tags
        };
        this.ingredients = data.ingredients.map(i => ({
          name: i.name,
          quantity: i.quantity,
          unit: i.unit,
          notes: i.notes
        }));
        this.steps = data.steps.map(s => ({
          instruction: s.instruction,
          timerSeconds: s.timerSeconds
        }));
        this.images = data.images;
        this.tagsInput = data.tags.join(', ');
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to load recipe';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  addIngredient() {
    this.ingredients.push({ name: '', quantity: '', unit: '', notes: '' });
  }

  removeIngredient(index: number) {
    this.ingredients.splice(index, 1);
  }

  addStep() {
    this.steps.push({ instruction: '', timerSeconds: undefined });
  }

  removeStep(index: number) {
    this.steps.splice(index, 1);
  }

  save() {
    if (!this.recipe.title.trim()) {
      this.error = 'Title is required';
      return;
    }

    const validIngredients = this.ingredients.filter(i => i.name.trim());
    const validSteps = this.steps.filter(s => s.instruction.trim());

    if (validIngredients.length === 0) {
      this.error = 'At least one ingredient is required';
      return;
    }

    if (validSteps.length === 0) {
      this.error = 'At least one step is required';
      return;
    }

    const tags = this.tagsInput.split(',').map(t => t.trim()).filter(t => t);

    const request: CreateRecipeRequest = {
      ...this.recipe,
      title: this.recipe.title.trim(),
      ingredients: validIngredients,
      steps: validSteps,
      tags
    };

    this.saving = true;
    this.error = '';

    const operation = this.isEdit && this.recipeId
      ? this.recipeService.updateRecipe(this.recipeId, request)
      : this.recipeService.createRecipe(request);

    operation.subscribe({
      next: (result) => {
        this.saving = false;
        this.router.navigate(['/recipes', result.id]);
      },
      error: () => {
        this.saving = false;
        this.error = 'Failed to save recipe';
        this.cdr.detectChanges();
      }
    });
  }

  cancel() {
    if (this.isEdit && this.recipeId) {
      this.router.navigate(['/recipes', this.recipeId]);
    } else {
      this.router.navigate(['/recipes']);
    }
  }

  onFileSelect(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length || !this.recipeId) return;

    const file = input.files[0];
    this.uploading = true;

    this.recipeService.uploadImage(this.recipeId, file).subscribe({
      next: (image) => {
        this.images.push(image);
        this.uploading = false;
        input.value = '';
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to upload image';
        this.uploading = false;
        this.cdr.detectChanges();
      }
    });
  }

  setAsTitleImage(imageId: string) {
    if (!this.recipeId) return;
    this.recipeService.setTitleImage(this.recipeId, imageId).subscribe({
      next: (images) => {
        this.images = images;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to set title image';
        this.cdr.detectChanges();
      }
    });
  }

  deleteImage(imageId: string) {
    if (!this.recipeId) return;
    if (!confirm('Delete this image?')) return;

    this.recipeService.deleteImage(this.recipeId, imageId).subscribe({
      next: () => {
        this.images = this.images.filter(i => i.id !== imageId);
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to delete image';
        this.cdr.detectChanges();
      }
    });
  }
}
