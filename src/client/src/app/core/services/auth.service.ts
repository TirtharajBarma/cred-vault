import { Injectable, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiResponse, User, RegisterRequest, VerifyOtpRequest, ResendVerificationRequest } from '../models/auth.models';

const API_GATEWAY = 'http://localhost:5006';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly baseUrl = `${API_GATEWAY}/api/v1/identity/auth`;
  private http = inject(HttpClient);
  private router = inject(Router);
  
  currentUser = signal<User | null>(this.getStoredUser());
  token = signal<string | null>(this.getStoredToken());
  pendingEmail = signal<string | null>(this.getPendingEmail());

  register(data: RegisterRequest): Observable<ApiResponse<any>> {
    console.log('[AuthService] Attempting registration for:', data.email);
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/register`, data).pipe(
      tap(response => {
        if (response.success) {
          this.setPendingEmail(data.email);
        }
      })
    );
  }

  verifyEmailOtp(data: VerifyOtpRequest): Observable<ApiResponse<any>> {
    console.log('[AuthService] Verifying OTP for:', data.email);
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/verify-email-otp`, data).pipe(
      tap(response => {
        if (response.success && response.data?.accessToken) {
          // Direct login flow: handle token from verification
          this.handleAuthSuccess(response.data);
          this.clearPendingEmail();
        }
      })
    );
  }

  resendVerification(data: ResendVerificationRequest): Observable<ApiResponse<any>> {
    console.log('[AuthService] Resending verification for:', data.email);
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/resend-verification`, data);
  }

  forgotPassword(email: string): Observable<ApiResponse<any>> {
    console.log('[AuthService] Forgot password request for:', email);
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/forgot-password`, { email }).pipe(
      tap(response => {
        if (response.success) {
          this.setPendingEmail(email);
        }
      })
    );
  }

  resetPassword(data: { email: string, otp: string, newPassword: string }): Observable<ApiResponse<any>> {
    console.log('[AuthService] Resetting password for:', data.email);
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/reset-password`, data);
  }

  login(credentials: { email: string, password: string }): Observable<ApiResponse<any>> {
    console.log('[AuthService] Attempting login for:', credentials.email);
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/login`, credentials).pipe(
      tap(response => {
        if (response.success && response.data) {
          this.handleAuthSuccess(response.data);
        }
      })
    );
  }

  logout(): void {
    console.log('[AuthService] Logging out...');
    this.clearAuthData();
    this.router.navigate(['/login'], { replaceUrl: true });
  }

  private handleAuthSuccess(data: any): void {
    const token = data.accessToken || data.AccessToken;
    const user = data.user || data.User;

    if (token && user) {
      console.log('[AuthService] Login success for user:', user.email);
      this.currentUser.set(user);
      this.token.set(token);
      localStorage.setItem('cv_token', token);
      localStorage.setItem('cv_user', JSON.stringify(user));
    }
  }

  private clearAuthData(): void {
    this.currentUser.set(null);
    this.token.set(null);
    localStorage.removeItem('cv_token');
    localStorage.removeItem('cv_user');
  }

  private setPendingEmail(email: string): void {
    this.pendingEmail.set(email);
    localStorage.setItem('cv_pending_email', email);
  }

  private clearPendingEmail(): void {
    this.pendingEmail.set(null);
    localStorage.removeItem('cv_pending_email');
  }

  private getPendingEmail(): string | null {
    return localStorage.getItem('cv_pending_email');
  }

  private getStoredToken(): string | null {
    return localStorage.getItem('cv_token');
  }

  private getStoredUser(): User | null {
    const userStr = localStorage.getItem('cv_user');
    try {
      return userStr ? JSON.parse(userStr) : null;
    } catch {
      return null;
    }
  }
}
