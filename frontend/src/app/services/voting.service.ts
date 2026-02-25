import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { VotingRound, Nomination, VotingRoundSummary } from '../models/voting.model';
import { PagedResult } from '../models/recipe.model';

@Injectable({ providedIn: 'root' })
export class VotingService {
  private apiUrl = `${environment.apiUrl}/voting`;

  constructor(private http: HttpClient) {}

  createRound(): Observable<VotingRound> {
    return this.http.post<VotingRound>(`${this.apiUrl}/rounds`, {});
  }

  getActiveRound(): Observable<VotingRound | null> {
    return this.http.get<VotingRound | null>(`${this.apiUrl}/rounds/active`);
  }

  nominate(roundId: string, recipeId: string): Observable<Nomination> {
    return this.http.post<Nomination>(`${this.apiUrl}/rounds/${roundId}/nominations`, { recipeId });
  }

  withdrawNomination(roundId: string, recipeId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/rounds/${roundId}/nominations/${recipeId}`);
  }

  vote(roundId: string, recipeId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/rounds/${roundId}/votes`, { recipeId });
  }

  closeRound(roundId: string): Observable<VotingRound> {
    return this.http.post<VotingRound>(`${this.apiUrl}/rounds/${roundId}/close`, {});
  }

  getRoundHistory(params: { page?: number; pageSize?: number } = {}): Observable<PagedResult<VotingRoundSummary>> {
    let httpParams = new HttpParams();
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<VotingRoundSummary>>(`${this.apiUrl}/rounds`, { params: httpParams });
  }
}
