import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register/register.component').then(m => m.RegisterComponent)
  },
  {
    path: 'verify-email',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/verify/verify-email.component').then(m => m.VerifyEmailComponent)
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'forgot-password',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent)
  },
  {
    path: 'reset-password',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/reset-password/reset-password.component').then(m => m.ResetPasswordComponent)
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
  },
  {
    path: 'cards/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/cards/card-details/card-details.component').then(m => m.CardDetailsComponent)
  },
  {
    path: 'admin',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/admin-layout/admin-layout.component').then(m => m.AdminLayoutComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./features/admin/dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent)
      },
      {
        path: 'users',
        loadComponent: () => import('./features/admin/users/user-management.component').then(m => m.UserManagementComponent)
      },
      {
        path: 'issuers',
        loadComponent: () => import('./features/admin/issuers/issuer-management.component').then(m => m.IssuerManagementComponent)
      },
      {
        path: 'bills',
        loadComponent: () => import('./features/admin/bills/bill-generation.component').then(m => m.BillGenerationComponent)
      },
      {
        path: 'rewards',
        loadComponent: () => import('./features/admin/rewards/reward-tiers.component').then(m => m.RewardTiersComponent)
      },
      {
        path: 'logs',
        loadComponent: () => import('./features/admin/logs/system-logs.component').then(m => m.SystemLogsComponent)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
