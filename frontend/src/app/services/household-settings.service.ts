import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface HouseholdAiSettings {
  aiProvider?: string;
  aiModel?: string;
  hasApiKey: boolean;
}

export interface HouseholdMember {
  id: string;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
}

export interface HouseholdSummary {
  id: string;
  name: string;
  inviteCode: string;
  members: HouseholdMember[];
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

  getHousehold(): Observable<HouseholdSummary> {
    return this.http.get<HouseholdSummary>(`${environment.apiUrl}/household/me`);
  }

  disableMember(userId: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/household/members/${userId}/disable`, {});
  }

  enableMember(userId: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/household/members/${userId}/enable`, {});
  }
}
