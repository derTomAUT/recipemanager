import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { VotingRound, Nomination, VotingRoundSummary } from '../models/voting.model';
import { PagedResult } from '../models/recipe.model';
import { resolveImageUrl } from '../utils/url.utils';

@Injectable({ providedIn: 'root' })
export class VotingService {
  private apiUrl = `${environment.apiUrl}/voting`;

  constructor(private http: HttpClient) {}

  createRound(): Observable<VotingRound> {
    return this.http.post<VotingRound>(`${this.apiUrl}/rounds`, {})
      .pipe(map(round => this.mapRound(round)));
  }

  getActiveRound(): Observable<VotingRound | null> {
    return this.http.get<VotingRound | null>(`${this.apiUrl}/rounds/active`)
      .pipe(map(round => round ? this.mapRound(round) : null));
  }

  nominate(roundId: string, recipeId: string): Observable<Nomination> {
    return this.http.post<Nomination>(`${this.apiUrl}/rounds/${roundId}/nominations`, { recipeId })
      .pipe(map(nomination => this.mapNomination(nomination)));
  }

  withdrawNomination(roundId: string, recipeId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/rounds/${roundId}/nominations/${recipeId}`);
  }

  vote(roundId: string, recipeId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/rounds/${roundId}/votes`, { recipeId });
  }

  closeRound(roundId: string): Observable<VotingRound> {
    return this.http.post<VotingRound>(`${this.apiUrl}/rounds/${roundId}/close`, {})
      .pipe(map(round => this.mapRound(round)));
  }

  getRoundHistory(params: { page?: number; pageSize?: number } = {}): Observable<PagedResult<VotingRoundSummary>> {
    let httpParams = new HttpParams();
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get<PagedResult<VotingRoundSummary>>(`${this.apiUrl}/rounds`, { params: httpParams })
      .pipe(map(result => ({
        ...result,
        items: result.items.map(item => this.mapRoundSummary(item))
      })));
  }

  private mapRound(round: VotingRound): VotingRound {
    return {
      ...round,
      nominations: round.nominations.map(n => this.mapNomination(n))
    };
  }

  private mapNomination(nomination: Nomination): Nomination {
    return {
      ...nomination,
      recipeImageUrl: resolveImageUrl(nomination.recipeImageUrl)
    };
  }

  private mapRoundSummary(summary: VotingRoundSummary): VotingRoundSummary {
    return {
      ...summary,
      winnerImageUrl: resolveImageUrl(summary.winnerImageUrl)
    };
  }
}
