import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { Recipe, RecipeDetail, RecipeImage, PagedResult, CreateRecipeRequest, UpdateRecipeRequest, CookEvent, MealAssistantResponse } from '../models/recipe.model';
import { resolveImageUrl } from '../utils/url.utils';

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

    return this.http.get<PagedResult<Recipe>>(this.apiUrl, { params: httpParams })
      .pipe(map(result => ({
        ...result,
        items: result.items.map(r => this.mapRecipe(r))
      })));
  }

  getRecipe(id: string): Observable<RecipeDetail> {
    return this.http.get<RecipeDetail>(`${this.apiUrl}/${id}`)
      .pipe(map(recipe => this.mapRecipeDetail(recipe)));
  }

  deleteRecipe(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  createRecipe(recipe: CreateRecipeRequest): Observable<RecipeDetail> {
    return this.http.post<RecipeDetail>(this.apiUrl, recipe)
      .pipe(map(created => this.mapRecipeDetail(created)));
  }

  updateRecipe(id: string, recipe: UpdateRecipeRequest): Observable<RecipeDetail> {
    return this.http.put<RecipeDetail>(`${this.apiUrl}/${id}`, recipe)
      .pipe(map(updated => this.mapRecipeDetail(updated)));
  }

  uploadImage(recipeId: string, file: File): Observable<RecipeImage> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<RecipeImage>(`${this.apiUrl}/${recipeId}/images`, formData)
      .pipe(map(image => this.mapImage(image)));
  }

  setTitleImage(recipeId: string, imageId: string): Observable<RecipeImage[]> {
    return this.http.patch<RecipeImage[]>(`${this.apiUrl}/${recipeId}/title-image`, { imageId })
      .pipe(map(images => images.map(img => this.mapImage(img))));
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
    return this.http.get<PagedResult<Recipe>>(`${this.apiUrl}/favorites`, { params: httpParams })
      .pipe(map(result => ({
        ...result,
        items: result.items.map(r => this.mapRecipe(r))
      })));
  }

  getRecommended(count: number = 10): Observable<Recipe[]> {
    return this.http.get<Recipe[]>(`${this.apiUrl}/recommended?count=${count}`)
      .pipe(map(recipes => recipes.map(r => this.mapRecipe(r))));
  }

  markCooked(recipeId: string, servings?: number): Observable<CookEvent> {
    return this.http.post<CookEvent>(`${this.apiUrl}/${recipeId}/cook`, { servings })
      .pipe(map(event => this.mapCookEvent(event)));
  }

  getRecipeCookHistory(recipeId: string): Observable<CookEvent[]> {
    return this.http.get<CookEvent[]>(`${this.apiUrl}/${recipeId}/cook-history`)
      .pipe(map(events => events.map(e => this.mapCookEvent(e))));
  }

  getCookHistory(params: { page?: number; pageSize?: number } = {}): Observable<PagedResult<CookEvent>> {
    let httpParams = new HttpParams();
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<CookEvent>>(`${environment.apiUrl}/cook-history`, { params: httpParams })
      .pipe(map(result => ({
        ...result,
        items: result.items.map(e => this.mapCookEvent(e))
      })));
  }

  getMealAssistantSuggestions(prompt: string): Observable<MealAssistantResponse> {
    return this.http.post<MealAssistantResponse>(`${this.apiUrl}/meal-assistant`, { prompt }).pipe(
      map(result => ({
        ...result,
        suggestions: result.suggestions.map(s => ({
          ...s,
          titleImageUrl: resolveImageUrl(s.titleImageUrl)
        }))
      }))
    );
  }

  estimateNutrition(recipeId: string): Observable<RecipeDetail> {
    return this.http.post<RecipeDetail>(`${this.apiUrl}/${recipeId}/nutrition/estimate`, {})
      .pipe(map(recipe => this.mapRecipeDetail(recipe)));
  }

  private mapRecipe(recipe: Recipe): Recipe {
    return {
      ...recipe,
      titleImageUrl: resolveImageUrl(recipe.titleImageUrl)
    };
  }

  private mapRecipeDetail(recipe: RecipeDetail): RecipeDetail {
    return {
      ...recipe,
      titleImageUrl: resolveImageUrl(recipe.titleImageUrl),
      images: recipe.images.map(img => this.mapImage(img))
    };
  }

  private mapImage(image: RecipeImage): RecipeImage {
    return {
      ...image,
      url: resolveImageUrl(image.url) ?? image.url
    };
  }

  private mapCookEvent(event: CookEvent): CookEvent {
    return {
      ...event,
      recipeImageUrl: resolveImageUrl(event.recipeImageUrl)
    };
  }
}
