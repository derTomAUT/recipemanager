import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PaperCardCommitRequest, PaperCardCommitResponse, PaperCardParseResponse, PaperCardUpdateImagesResponse } from '../models/recipe.model';

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

  updateDraftImage(draftId: string, imageIndex: number, image: File): Observable<PaperCardUpdateImagesResponse> {
    const formData = new FormData();
    formData.append('draftId', draftId);
    formData.append('imageIndex', imageIndex.toString());
    formData.append('image', image);
    return this.http.post<PaperCardUpdateImagesResponse>(`${this.apiBase}/draft-image`, formData);
  }
}
