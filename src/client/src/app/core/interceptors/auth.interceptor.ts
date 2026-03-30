import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { catchError, throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  console.log('[AuthInterceptor] Intercepting request to:', req.url);
  const authService = inject(AuthService);
  const token = authService.token();

  // Attach token if available
  if (token) {
    console.log('[AuthInterceptor] Attaching token to:', req.url);
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  } else {
    console.warn('[AuthInterceptor] No token found in AuthService for:', req.url);
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Global 401 handler
      if (error.status === 401) {
        console.warn('[AuthInterceptor] 401 Unauthorized detected. Logging out...');
        authService.logout();
      }
      return throwError(() => error);
    })
  );
};
