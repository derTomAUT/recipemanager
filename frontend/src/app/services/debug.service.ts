import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AiDebugPagedResult, AiDebugQuery } from '../models/debug.model';

@Injectable({ providedIn: 'root' })
export class DebugService {
  private apiUrl = `${environment.apiUrl}/debug`;

  constructor(private http: HttpClient) {}

  getAiLogs(query: AiDebugQuery = {}): Observable<AiDebugPagedResult> {
    let params = new HttpParams();
    if (query.provider) params = params.set('provider', query.provider);
    if (query.operation) params = params.set('operation', query.operation);
    if (query.success !== undefined) params = params.set('success', String(query.success));
    if (query.page) params = params.set('page', String(query.page));
    if (query.pageSize) params = params.set('pageSize', String(query.pageSize));

    return this.http.get<AiDebugPagedResult>(`${this.apiUrl}/ai`, { params });
  }
}
