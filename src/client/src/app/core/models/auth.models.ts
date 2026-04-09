export enum UserRole {
  User = 'user',
  Admin = 'admin'
}

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: string;
  status?: string;
  isEmailVerified?: boolean;
  createdAtUtc?: string;
}

export interface AuthResult {
  accessToken: string;
  user: User;
  success: boolean;
  message: string;
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  traceId?: string;
  errorCode?: string;
}

export interface LoginResponse {
  accessToken: string;
  user: User;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
}

export interface VerifyOtpRequest {
  email: string;
  otp: string;
}

export interface ResendVerificationRequest {
  email: string;
}
