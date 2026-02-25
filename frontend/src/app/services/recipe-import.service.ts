import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { RecipeDraft } from '../models/recipe.model';

@Injectable({ providedIn: 'root' })
export class RecipeImportService {
  private apiUrl = `${environment.apiUrl}/recipes/import/url`;

  constructor(private http: HttpClient) {}

  importFromUrl(url: string): Observable<RecipeDraft> {
    return this.http.post<RecipeDraft>(this.apiUrl, { url });
  }
}
