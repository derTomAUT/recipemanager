import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, finalize, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../environments/environment';

interface User {
  id: string;
  email: string;
  name: string;
  profileImageUrl?: string;
  householdId?: string;
  role?: string;
}

interface AuthResponse {
  token: string;
  user: User;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private tokenKey = 'auth_token';
  private userSubject = new BehaviorSubject<User | null>(this.getStoredUser());
  public user$ = this.userSubject.asObservable();
  private refreshInFlight?: Observable<AuthResponse>;

  constructor(private http: HttpClient) {}

  googleLogin(idToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/google`, { idToken })
      .pipe(tap(res => this.storeAuth(res)));
  }

  refreshToken(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/refresh`, {})
      .pipe(tap(res => this.storeAuth(res)));
  }

  private storeAuth(res: AuthResponse) {
    localStorage.setItem(this.tokenKey, res.token);
    localStorage.setItem('user', JSON.stringify(res.user));
    this.userSubject.next(res.user);
  }

  logout() {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem('user');
    this.userSubject.next(null);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    return !!token && !this.isTokenExpired(token);
  }

  isTokenExpired(token: string): boolean {
    const payload = this.getTokenPayload(token);
    if (!payload?.exp) return true;
    const nowSeconds = Math.floor(Date.now() / 1000);
    return payload.exp <= nowSeconds;
  }

  getTokenExpiry(token: string): number | null {
    const payload = this.getTokenPayload(token);
    return payload?.exp ? payload.exp * 1000 : null;
  }

  refreshTokenIfNeeded(bufferSeconds: number = 300): Observable<AuthResponse | null> {
    const token = this.getToken();
    if (!token) return of(null);
    const expMs = this.getTokenExpiry(token);
    if (!expMs) return of(null);
    const now = Date.now();
    if (expMs - now > bufferSeconds * 1000) return of(null);
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.refreshToken().pipe(
        finalize(() => this.refreshInFlight = undefined),
        shareReplay(1)
      );
    }
    return this.refreshInFlight;
  }

  private getTokenPayload(token: string): { exp?: number } | null {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    try {
      const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const json = atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, '='));
      return JSON.parse(json);
    } catch {
      return null;
    }
  }

  private getStoredUser(): User | null {
    const user = localStorage.getItem('user');
    return user ? JSON.parse(user) : null;
  }
}
