import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';
import { CreditCard, CardTransaction, CreateCardRequest, CardIssuer } from '../models/card.models';
import { environment } from '../../../environments/environment';

const API_GATEWAY = environment.apiGatewayUrl;

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly baseUrl = `${API_GATEWAY}/api/v1/cards`;
  private http = inject(HttpClient);

  getCards(): Observable<ApiResponse<CreditCard[]>> {
    return this.http.get<ApiResponse<CreditCard[]>>(this.baseUrl);
  }

  getIssuers(): Observable<ApiResponse<CardIssuer[]>> {
    return this.http.get<ApiResponse<CardIssuer[]>>(`${API_GATEWAY}/api/v1/issuers`);
  }

  getAllTransactions(): Observable<ApiResponse<CardTransaction[]>> {
    return this.http.get<ApiResponse<CardTransaction[]>>(`${this.baseUrl}/transactions`);
  }

  getRewardAccount(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/rewards/account`);
  }

  getCardById(cardId: string): Observable<ApiResponse<CreditCard>> {
    return this.http.get<ApiResponse<CreditCard>>(`${this.baseUrl}/${cardId}`);
  }

  getTransactionsByCardId(cardId: string): Observable<ApiResponse<CardTransaction[]>> {
    return this.http.get<ApiResponse<CardTransaction[]>>(`${this.baseUrl}/${cardId}/transactions`);
  }

  addCard(request: CreateCardRequest): Observable<ApiResponse<CreditCard>> {
    return this.http.post<ApiResponse<CreditCard>>(this.baseUrl, request);
  }

  deleteCard(cardId: string): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.baseUrl}/${cardId}`);
  }

  addTransaction(cardId: string, request: { type: number, amount: number, description: string, dateUtc?: string }): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/${cardId}/transactions`, request);
  }
}
