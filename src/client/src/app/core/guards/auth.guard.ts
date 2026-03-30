import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.token()) {
    return true;
  }

  console.warn('[AuthGuard] Unauthorized access blocked. Redirecting to login.');
  router.navigate(['/login']);
  return false;
};
