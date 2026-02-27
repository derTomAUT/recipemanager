import { TestBed } from '@angular/core/testing';
import { RecipeEditorComponent } from './recipe-editor.component';
import { RecipeService } from '../../services/recipe.service';
import { RecipeDraftService } from '../../services/recipe-draft.service';
import { RecipeImportService } from '../../services/recipe-import.service';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';

describe('RecipeEditorComponent', () => {
  it('applies sourceUrl from draft', () => {
    TestBed.configureTestingModule({
      imports: [RecipeEditorComponent],
      providers: [
        { provide: RecipeService, useValue: {} },
        { provide: RecipeDraftService, useValue: {} },
        { provide: RecipeImportService, useValue: {} },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map() } } },
        { provide: Router, useValue: { navigate: () => {} } }
      ]
    });

    const fixture = TestBed.createComponent(RecipeEditorComponent);
    const component = fixture.componentInstance;

    component.applyDraft({
      title: 'Imported',
      description: 'Test',
      sourceUrl: 'https://example.com/recipe',
      servings: 2,
      prepMinutes: 10,
      cookMinutes: 20,
      ingredients: [],
      steps: [],
      tags: [],
      importedImages: [],
      candidateImages: [],
      confidenceScore: 0.8,
      warnings: []
    });

    expect(component.recipe.sourceUrl).toBe('https://example.com/recipe');
  });

  it('includes sourceUrl when saving a new recipe', () => {
    const createRecipe = jasmine.createSpy().and.returnValue(of({ id: '1' } as any));

    TestBed.configureTestingModule({
      imports: [RecipeEditorComponent],
      providers: [
        { provide: RecipeService, useValue: { createRecipe } },
        { provide: RecipeDraftService, useValue: {} },
        { provide: RecipeImportService, useValue: {} },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map() } } },
        { provide: Router, useValue: { navigate: () => {} } }
      ]
    });

    const fixture = TestBed.createComponent(RecipeEditorComponent);
    const component = fixture.componentInstance;

    component.isEdit = false;
    component.recipeId = null;
    component.recipe = {
      title: 'Test Recipe',
      description: 'Tasty',
      sourceUrl: 'https://example.com/source',
      servings: 2,
      prepMinutes: 5,
      cookMinutes: 10,
      ingredients: [],
      steps: [],
      tags: []
    };
    component.ingredients = [{ name: 'Salt', quantity: '1', unit: 'tsp', notes: '' }];
    component.steps = [{ instruction: 'Mix', timerSeconds: undefined }];
    component.tagsInput = 'test';

    component.save();

    const request = createRecipe.calls.mostRecent().args[0];
    expect(request.sourceUrl).toBe('https://example.com/source');
  });
});
