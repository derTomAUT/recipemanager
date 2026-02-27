import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { catchError, switchMap, throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.getToken();
  const isRefreshCall = req.url.includes('/auth/refresh');

  const proceed = () => {
    const currentToken = authService.getToken();
    if (currentToken && !authService.isTokenExpired(currentToken)) {
      req = req.clone({
        setHeaders: { Authorization: `Bearer ${currentToken}` }
      });
    }
    return next(req);
  };

  const request$ = isRefreshCall
    ? proceed()
    : authService.refreshTokenIfNeeded(300).pipe(switchMap(() => proceed()));

  return request$.pipe(
    catchError(err => {
      if (err.status === 401) {
        authService.logout();
        router.navigate(['/login']);
      }
      return throwError(() => err);
    })
  );
};
