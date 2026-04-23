import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';
import { environment } from '../../../environments/environment';

const API_GATEWAY = environment.apiGatewayUrl;

export interface WalletInfo {
  hasWallet: boolean;
  walletId?: string;
  balance: number;
  createdAtUtc?: string;
}

export interface WalletTransaction {
  id: string;
  type: number;
  typeName: string;
  amount: number;
  balanceAfter: number;
  description: string;
  relatedPaymentId?: string;
  createdAtUtc: string;
}

export interface TopUpRequest {
  amount: number;
  description?: string;
}

@Injectable({
  providedIn: 'root'
})
export class WalletService {
  private readonly baseUrl = `${API_GATEWAY}/api/v1/wallets`;
  private http = inject(HttpClient);

  getMyWallet(): Observable<ApiResponse<WalletInfo>> {
    return this.http.get<ApiResponse<WalletInfo>>(`${this.baseUrl}/me`);
  }

  getBalance(): Observable<ApiResponse<{ balance: number; hasWallet: boolean }>> {
    return this.http.get<ApiResponse<{ balance: number; hasWallet: boolean }>>(`${this.baseUrl}/balance`);
  }

  topUp(request: TopUpRequest): Observable<ApiResponse<{ amount: number; newBalance: number; description: string }>> {
    return this.http.post<ApiResponse<{ amount: number; newBalance: number; description: string }>>(`${this.baseUrl}/topup`, request);
  }

  getTransactions(skip = 0, take = 20): Observable<ApiResponse<WalletTransaction[]>> {
    return this.http.get<ApiResponse<WalletTransaction[]>>(`${this.baseUrl}/transactions?skip=${skip}&take=${take}`);
  }
}
