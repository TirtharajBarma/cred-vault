import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const currentUser = authService.currentUser();

  if (currentUser && currentUser.role === 'admin') {
    return true;
  }

  console.warn('[AdminGuard] Access denied. Admin role required.');
  router.navigate(['/dashboard']);
  return false;
};
