import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PaperCardCommitRequest, PaperCardCommitResponse, PaperCardParseResponse } from '../models/recipe.model';

@Injectable({ providedIn: 'root' })
export class PaperCardImportService {
  private apiBase = `${environment.apiUrl}/import/paper-card`;

  constructor(private http: HttpClient) {}

  parse(frontImage: File, backImage: File): Observable<PaperCardParseResponse> {
    const formData = new FormData();
    formData.append('frontImage', frontImage);
    formData.append('backImage', backImage);
    return this.http.post<PaperCardParseResponse>(`${this.apiBase}/parse`, formData);
  }

  commit(request: PaperCardCommitRequest): Observable<PaperCardCommitResponse> {
    return this.http.post<PaperCardCommitResponse>(`${this.apiBase}/commit`, request);
  }
}
