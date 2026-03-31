import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';

const API_GATEWAY = 'http://localhost:5006';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private http = inject(HttpClient);
  
  // Identity Admin
  getUserDetails(userId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users/${userId}`);
  }

  updateUserStatus(userId: string, status: number): Observable<ApiResponse<any>> {
    return this.http.put<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users/${userId}/status`, { status });
  }

  getAllUsers(params: { page?: number; pageSize?: number; search?: string; status?: string }): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users`, { params });
  }

  getUserStats(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users/stats`);
  }

  // Card Admin
  getIssuers(): Observable<ApiResponse<any[]>> {
    return this.http.get<ApiResponse<any[]>>(`${API_GATEWAY}/api/v1/issuers`);
  }

  createIssuer(issuer: any): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${API_GATEWAY}/api/v1/issuers`, issuer);
  }

  deleteIssuer(id: string): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${API_GATEWAY}/api/v1/issuers/${id}`);
  }

  updateCardLimit(cardId: string, limitData: any): Observable<ApiResponse<any>> {
    return this.http.put<ApiResponse<any>>(`${API_GATEWAY}/api/v1/cards/${cardId}/admin`, limitData);
  }

  // Billing Admin
  generateBill(billData: any): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/bills/admin/generate-bill`, billData);
  }

  getRewardTiers(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/rewards/tiers`);
  }

  createRewardTier(tier: any): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/rewards/tiers`, tier);
  }

  deleteRewardTier(id: string): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/rewards/tiers/${id}`);
  }

  // Notification Admin
  getAuditLogs(params?: any): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/notifications/audit`, { params });
  }

  getNotificationLogs(params?: any): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/notifications/logs`, { params });
  }

  // Helper methods for dropdowns
  getAllUsersForDropdown(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users`, { params: { pageSize: 1000 } });
  }

  getCardsByUser(userId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/cards/user/${userId}`);
  }
}