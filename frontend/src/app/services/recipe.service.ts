import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Recipe, RecipeDetail, RecipeImage, PagedResult, CreateRecipeRequest, UpdateRecipeRequest, CookEvent } from '../models/recipe.model';

@Injectable({ providedIn: 'root' })
export class RecipeService {
  private apiUrl = `${environment.apiUrl}/recipes`;

  constructor(private http: HttpClient) {}

  getRecipes(params: {
    search?: string;
    tags?: string;
    page?: number;
    pageSize?: number;
  } = {}): Observable<PagedResult<Recipe>> {
    let httpParams = new HttpParams();
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.tags) httpParams = httpParams.set('tags', params.tags);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<PagedResult<Recipe>>(this.apiUrl, { params: httpParams });
  }

  getRecipe(id: string): Observable<RecipeDetail> {
    return this.http.get<RecipeDetail>(`${this.apiUrl}/${id}`);
  }

  deleteRecipe(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  createRecipe(recipe: CreateRecipeRequest): Observable<RecipeDetail> {
    return this.http.post<RecipeDetail>(this.apiUrl, recipe);
  }

  updateRecipe(id: string, recipe: UpdateRecipeRequest): Observable<RecipeDetail> {
    return this.http.put<RecipeDetail>(`${this.apiUrl}/${id}`, recipe);
  }

  uploadImage(recipeId: string, file: File): Observable<RecipeImage> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<RecipeImage>(`${this.apiUrl}/${recipeId}/images`, formData);
  }

  setTitleImage(recipeId: string, imageId: string): Observable<RecipeImage[]> {
    return this.http.patch<RecipeImage[]>(`${this.apiUrl}/${recipeId}/title-image`, { imageId });
  }

  deleteImage(recipeId: string, imageId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${recipeId}/images/${imageId}`);
  }

  addFavorite(recipeId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${recipeId}/favorite`, {});
  }

  removeFavorite(recipeId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${recipeId}/favorite`);
  }

  getFavorites(params: { page?: number; pageSize?: number } = {}): Observable<PagedResult<Recipe>> {
    let httpParams = new HttpParams();
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<Recipe>>(`${this.apiUrl}/favorites`, { params: httpParams });
  }

  getRecommended(count: number = 10): Observable<Recipe[]> {
    return this.http.get<Recipe[]>(`${this.apiUrl}/recommended?count=${count}`);
  }

  markCooked(recipeId: string, servings?: number): Observable<CookEvent> {
    return this.http.post<CookEvent>(`${this.apiUrl}/${recipeId}/cook`, { servings });
  }

  getRecipeCookHistory(recipeId: string): Observable<CookEvent[]> {
    return this.http.get<CookEvent[]>(`${this.apiUrl}/${recipeId}/cook-history`);
  }

  getCookHistory(params: { page?: number; pageSize?: number } = {}): Observable<PagedResult<CookEvent>> {
    let httpParams = new HttpParams();
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<CookEvent>>(`${environment.apiUrl}/cook-history`, { params: httpParams });
  }
}
