import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { UserPreferences } from '../models/preference.model';

@Injectable({ providedIn: 'root' })
export class PreferencesService {
  private apiUrl = `${environment.apiUrl}/preferences`;

  constructor(private http: HttpClient) {}

  getPreferences(): Observable<UserPreferences> {
    return this.http.get<UserPreferences>(this.apiUrl);
  }

  updatePreferences(prefs: UserPreferences): Observable<UserPreferences> {
    return this.http.put<UserPreferences>(this.apiUrl, prefs);
  }
}
