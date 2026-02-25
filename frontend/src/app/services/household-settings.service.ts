import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface HouseholdAiSettings {
  aiProvider?: string;
  aiModel?: string;
  hasApiKey: boolean;
}

export interface UpdateHouseholdAiSettingsRequest {
  aiProvider?: string;
  aiModel?: string;
  apiKey?: string;
}

@Injectable({ providedIn: 'root' })
export class HouseholdSettingsService {
  constructor(private http: HttpClient) {}

  getSettings(): Observable<HouseholdAiSettings> {
    return this.http.get<HouseholdAiSettings>(`${environment.apiUrl}/household/settings`);
  }

  updateSettings(request: UpdateHouseholdAiSettingsRequest): Observable<HouseholdAiSettings> {
    return this.http.put<HouseholdAiSettings>(`${environment.apiUrl}/household/settings`, request);
  }

  getModels(provider: string): Observable<string[]> {
    return this.http.get<string[]>(`${environment.apiUrl}/ai/providers/models`, { params: { provider } });
  }
}
