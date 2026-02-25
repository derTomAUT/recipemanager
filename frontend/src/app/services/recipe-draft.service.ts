import { Injectable } from '@angular/core';
import { RecipeDraft } from '../models/recipe.model';

@Injectable({ providedIn: 'root' })
export class RecipeDraftService {
  private draft: RecipeDraft | null = null;

  setDraft(draft: RecipeDraft) {
    this.draft = draft;
  }

  consumeDraft(): RecipeDraft | null {
    const value = this.draft;
    this.draft = null;
    return value;
  }
}
