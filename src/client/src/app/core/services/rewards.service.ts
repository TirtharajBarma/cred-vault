import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';

const API_GATEWAY = 'http://localhost:5006';

export interface Statement {
  id: string;
  billId: string | null;
  cardId: string;
  statementPeriod: string;
  cardLast4: string;
  cardNetwork: string;
  issuerName: string;
  closingBalance: number;
  minimumDue: number;
  amountPaid: number;
  status: number;
  periodEndUtc: string;
  dueDateUtc: string | null;
}

export interface StatementDetail {
  id: string;
  userId: string;
  cardId: string;
  statementPeriod: string;
  cardLast4: string;
  cardNetwork: string;
  issuerName: string;
  openingBalance: number;
  totalPurchases: number;
  totalPayments: number;
  totalRefunds: number;
  penaltyCharges: number;
  interestCharges: number;
  closingBalance: number;
  minimumDue: number;
  amountPaid: number;
  status: number;
  periodStartUtc: string;
  periodEndUtc: string;
  generatedAtUtc: string;
  dueDateUtc: string | null;
  paidAtUtc: string | null;
  creditLimit: number;
  availableCredit: number;
  transactions: StatementTransaction[];
}

export interface StatementTransaction {
  id: string;
  type: string;
  amount: number;
  description: string;
  dateUtc: string;
}

export interface RewardAccount {
  id: string;
  userId: string;
  rewardTierId: string;
  pointsBalance: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface RewardTransaction {
  id: string;
  rewardAccountId: string;
  billId: string;
  points: number;
  type: number;
  createdAtUtc: string;
  reversedAtUtc: string | null;
}

export interface RewardTier {
  id: string;
  cardNetwork: number;
  issuerId: string | null;
  minSpend: number;
  rewardRate: number;
  effectiveFromUtc: string;
  effectiveToUtc: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class RewardsService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${API_GATEWAY}/api/v1/billing`;

  getRewardAccount(): Observable<ApiResponse<RewardAccount>> {
    return this.http.get<ApiResponse<RewardAccount>>(`${this.baseUrl}/rewards/account`);
  }

  getRewardTiers(): Observable<ApiResponse<RewardTier[]>> {
    return this.http.get<ApiResponse<RewardTier[]>>(`${this.baseUrl}/rewards/tiers`);
  }

  getRewardHistory(): Observable<ApiResponse<RewardTransaction[]>> {
    return this.http.get<ApiResponse<RewardTransaction[]>>(`${this.baseUrl}/rewards/transactions`);
  }
}

@Injectable({
  providedIn: 'root'
})
export class StatementService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${API_GATEWAY}/api/v1/billing/statements`;

  getMyStatements(): Observable<ApiResponse<Statement[]>> {
    return this.http.get<ApiResponse<Statement[]>>(this.baseUrl);
  }

  getStatementById(statementId: string): Observable<ApiResponse<StatementDetail>> {
    return this.http.get<ApiResponse<StatementDetail>>(`${this.baseUrl}/${statementId}`);
  }

  getStatementByBillId(billId: string): Observable<ApiResponse<Statement | Statement[]>> {
    return this.http.get<ApiResponse<Statement | Statement[]>>(`${this.baseUrl}/bill/${billId}`);
  }

  generateStatement(cardId: string): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/generate`, { cardId });
  }
}
