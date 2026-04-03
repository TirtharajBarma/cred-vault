import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';
import { environment } from '../../../environments/environment';

const API_GATEWAY = environment.apiGatewayUrl;

export interface Bill {
  id: string;
  userId: string;
  cardId: string;
  cardNetwork: number;
  issuerId: string;
  amount: number;
  minDue: number;
  currency: string;
  billingDateUtc: string;
  dueDateUtc: string;
  amountPaid?: number;
  paidAtUtc?: string;
  status: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export enum BillStatus {
  Pending = 1,
  Paid = 2,
  Overdue = 3,
  Cancelled = 4,
  PartiallyPaid = 5
}

@Injectable({
  providedIn: 'root'
})
export class BillingService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${API_GATEWAY}/api/v1/billing/bills`;

  getMyBills(): Observable<ApiResponse<Bill[]>> {
    return this.http.get<ApiResponse<Bill[]>>(this.baseUrl);
  }

  getBillById(billId: string): Observable<ApiResponse<Bill>> {
    return this.http.get<ApiResponse<Bill>>(`${this.baseUrl}/${billId}`);
  }

  getBillStatusLabel(status: number): string {
    return BillStatus[status] || 'Unknown';
  }

  getBillStatusClass(status: number): string {
    switch (status) {
      case BillStatus.Pending: return 'badge-warning';
      case BillStatus.Paid: return 'badge-success';
      case BillStatus.Overdue: return 'badge-error';
      case BillStatus.PartiallyPaid: return 'badge-info';
      case BillStatus.Cancelled: return 'badge-neutral';
      default: return 'badge-neutral';
    }
  }

  getStatementTransactions(statementId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${API_GATEWAY}/api/v1/billing/statements/${statementId}/transactions`);
  }
}
