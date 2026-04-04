import { Injectable, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiResponse, User, RegisterRequest, VerifyOtpRequest, ResendVerificationRequest } from '../models/auth.models';
import { environment } from '../../../environments/environment';

const API_GATEWAY = environment.apiGatewayUrl;

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
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/register`, data).pipe(
      tap(response => {
        if (response.success) {
          this.setPendingEmail(data.email);
        }
      })
    );
  }

  verifyEmailOtp(data: VerifyOtpRequest): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/verify-email-otp`, data).pipe(
      tap(response => {
        if (response.success && response.data?.accessToken) {
          this.handleAuthSuccess(response.data);
          this.clearPendingEmail();
        }
      })
    );
  }

  resendVerification(data: ResendVerificationRequest): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/resend-verification`, data);
  }

  forgotPassword(email: string): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/forgot-password`, { email }).pipe(
      tap(response => {
        if (response.success) {
          this.setPendingEmail(email);
        }
      })
    );
  }

  resetPassword(data: { email: string, otp: string, newPassword: string }): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/reset-password`, data);
  }

  getProfile(): Observable<ApiResponse<User>> {
    return this.http.get<ApiResponse<User>>(`${API_GATEWAY}/api/v1/identity/users/me`);
  }

  updateProfile(data: { fullName?: string }): Observable<ApiResponse<User>> {
    return this.http.put<ApiResponse<User>>(`${API_GATEWAY}/api/v1/identity/users/me`, data);
  }

  changePassword(data: { currentPassword: string, newPassword: string }): Observable<ApiResponse<any>> {
    return this.http.put<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users/me/password`, data);
  }

  login(credentials: { email: string, password: string }): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/login`, credentials).pipe(
      tap(response => {
        if (response.success && response.data) {
          this.handleAuthSuccess(response.data);
        }
      })
    );
  }

  loginWithGoogle(idToken: string): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/google`, { idToken }).pipe(
      tap(response => {
        if (response.success && response.data) {
          this.handleAuthSuccess(response.data);
        }
      })
    );
  }

  logout(): void {
    this.clearAuthData();
    this.router.navigate(['/login'], { replaceUrl: true });
  }

  private handleAuthSuccess(data: any): void {
    const token = data.accessToken || data.AccessToken;
    const user = data.user || data.User;

    if (token && user) {
      this.currentUser.set(user);
      this.token.set(token);
      sessionStorage.setItem('cv_token', token);
      sessionStorage.setItem('cv_user', JSON.stringify(user));
    }
  }

  private clearAuthData(): void {
    this.currentUser.set(null);
    this.token.set(null);
    sessionStorage.removeItem('cv_token');
    sessionStorage.removeItem('cv_user');
  }

  private setPendingEmail(email: string): void {
    this.pendingEmail.set(email);
    sessionStorage.setItem('cv_pending_email', email);
  }

  private clearPendingEmail(): void {
    this.pendingEmail.set(null);
    sessionStorage.removeItem('cv_pending_email');
  }

  private getPendingEmail(): string | null {
    return sessionStorage.getItem('cv_pending_email');
  }

  private getStoredToken(): string | null {
    return sessionStorage.getItem('cv_token');
  }

  private getStoredUser(): User | null {
    const userStr = sessionStorage.getItem('cv_user');
    try {
      return userStr ? JSON.parse(userStr) : null;
    } catch {
      return null;
    }
  }
}
