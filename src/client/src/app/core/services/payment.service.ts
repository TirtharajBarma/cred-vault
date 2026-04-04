import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';
import { environment } from '../../../environments/environment';

const API_GATEWAY = environment.apiGatewayUrl;

export interface Payment {
  id: string;
  userId: string;
  cardId: string;
  billId: string;
  amount: number;
  paymentType: string;
  status: string;
  failureReason?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PaymentInitiateRequest {
  cardId: string;
  billId: string;
  amount: number;
  paymentType: 'Full' | 'Partial';
  rewardsPoints?: number | null;
}

export interface PaymentInitiateResponse {
  paymentId: string;
  otpRequired: boolean;
  status: string;
  rewardsApplied?: boolean;
  rewardsAmount?: number;
  finalAmount?: number;
}

@Injectable({
  providedIn: 'root'
})
export class PaymentService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${API_GATEWAY}/api/v1/payments`;

  initiatePayment(request: PaymentInitiateRequest): Observable<ApiResponse<PaymentInitiateResponse>> {
    return this.http.post<ApiResponse<PaymentInitiateResponse>>(`${this.baseUrl}/initiate`, request);
  }

  verifyOtp(paymentId: string, otpCode: string): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/${paymentId}/verify-otp`, { otpCode });
  }

  resendOtp(paymentId: string): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/${paymentId}/resend-otp`, {});
  }

  getMyPayments(): Observable<ApiResponse<Payment[]>> {
    return this.http.get<ApiResponse<Payment[]>>(this.baseUrl);
  }
}
