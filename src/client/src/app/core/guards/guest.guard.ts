import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.token()) {
    return true;
  }

  console.warn('[GuestGuard] Authenticated user blocked from login/register. Redirecting to dashboard.');
  router.navigate(['/dashboard']);
  return false;
};
