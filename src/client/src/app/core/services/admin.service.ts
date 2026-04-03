import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';

const API_GATEWAY = 'http://localhost:5006';

export interface User {
  id: string;
  email: string;
  fullName: string;
  status: string;
  role: string;
  createdAtUtc: string;
  isEmailVerified: boolean;
  cardCount?: number;
}

export interface UserStats {
  totalUsers: number;
  activeUsers: number;
  suspendedUsers: number;
  pendingUsers: number;
  blockedUsers: number;
}

export interface AuditLog {
  id: string;
  entityName: string;
  entityId: string;
  action: string;
  userId: string;
  traceId: string;
  isSuccess: boolean;
  createdAtUtc: string;
}

export interface NotificationLog {
  id: string;
  subject: string;
  type: string;
  recipient: string;
  isSuccess: boolean;
  errorMessage?: string;
  traceId: string;
  createdAtUtc: string;
}

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private http = inject(HttpClient);
  
  // Identity Admin
  getUserDetails(userId: string): Observable<ApiResponse<User>> {
    return this.http.get<ApiResponse<User>>(`${API_GATEWAY}/api/v1/identity/users/${userId}`);
  }

  updateUserStatus(userId: string, status: string): Observable<ApiResponse<any>> {
    return this.http.put<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users/${userId}/status`, { Status: status });
  }

  updateUserRole(userId: string, role: string): Observable<ApiResponse<any>> {
    return this.http.put<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users/${userId}/role`, { Role: role });
  }

  getAllUsers(params: { page?: number; pageSize?: number; search?: string; status?: string } = {}): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users`, { params: { ...params } });
  }

  getUserStats(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users/stats`);
  }

  // Card Admin
  getIssuers(): Observable<ApiResponse<any[]>> {
    return this.http.get<ApiResponse<any[]>>(`${API_GATEWAY}/api/v1/issuers`);
  }

  createIssuer(issuer: any): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${API_GATEWAY}/api/v1/issuers`, {
      Name: issuer.name,
      Network: issuer.network
    });
  }

  updateIssuer(id: string, issuer: any): Observable<ApiResponse<any>> {
    return this.http.put<ApiResponse<any>>(`${API_GATEWAY}/api/v1/issuers/${id}`, {
      Name: issuer.name,
      Network: issuer.network
    });
  }

  deleteIssuer(id: string): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${API_GATEWAY}/api/v1/issuers/${id}`);
  }


  // Billing Admin
  generateBill(billData: { userId: string; cardId: string; currency: string }): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/bills/admin/generate-bill`, {
      UserId: billData.userId,
      CardId: billData.cardId,
      Currency: billData.currency
    });
  }

  getRewardTiers(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/rewards/tiers`);
  }

  createRewardTier(tier: any): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/rewards/tiers`, {
      CardNetwork: tier.cardNetwork === 1 ? 'Visa' : 'Mastercard',
      IssuerId: tier.issuerId || null,
      MinSpend: tier.minSpend,
      RewardRate: tier.rewardRate,
      EffectiveFromUtc: tier.effectiveFromUtc ? new Date(tier.effectiveFromUtc).toISOString() : new Date().toISOString(),
      EffectiveToUtc: tier.effectiveToUtc ? new Date(tier.effectiveToUtc).toISOString() : null
    });
  }

  deleteRewardTier(id: string): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/rewards/tiers/${id}`);
  }

  // Notification Admin
  getAuditLogs(params: { page?: number; pageSize?: number; traceId?: string } = {}): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/notifications/audit`, { params: { ...params } });
  }

  getNotificationLogs(params: { page?: number; pageSize?: number; email?: string } = {}): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/notifications/logs`, { params: { ...params } });
  }

  // Helper methods for dropdowns
  getAllUsersForDropdown(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/identity/users`, { params: { pageSize: 1000 } });
  }

  getCardsByUser(userId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/cards/user/${userId}`);
  }

  updateCardByAdmin(cardId: string, cardData: { creditLimit: number; outstandingBalance?: number | null; billingCycleStartDay?: number | null }): Observable<ApiResponse<any>> {
    return this.http.put<ApiResponse<any>>(`${API_GATEWAY}/api/v1/cards/${cardId}/admin`, {
      CreditLimit: cardData.creditLimit,
      OutstandingBalance: cardData.outstandingBalance ?? null,
      BillingCycleStartDay: cardData.billingCycleStartDay ?? null
    });
  }

  // Check Overdue Bills
  checkOverdue(): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/bills/admin/check-overdue`, {});
  }

  // User-specific data (reuses existing endpoints with userId filter)
  getUserBills(userId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/bills`, { params: { userId } });
  }

  getUserStatements(userId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/statements`, { params: { userId } });
  }

  getUserNotificationLogs(email: string, page: number = 1, pageSize: number = 200): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/notifications/logs`, { params: { email, page, pageSize } });
  }

  getUserAuditLogs(userId: string, page: number = 1, pageSize: number = 200): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/notifications/audit`, { params: { userId, page, pageSize } });
  }

  getCardStatements(cardId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/statements`, { params: { cardId } });
  }

  getAllStatementsForAdmin(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/statements/admin/all`);
  }

  getAdminCardTransactions(cardId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/cards/admin/${cardId}/transactions`);
  }

  getUserRewardTransactions(userId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/rewards/transactions`, { params: { userId } });
  }

  getAdminStatementFull(statementId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/statements/admin/${statementId}/full`);
  }
}
