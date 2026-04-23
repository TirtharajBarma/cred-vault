import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { StatementService, StatementDetail } from '../../../core/services/rewards.service';
import { DashboardService } from '../../../core/services/dashboard.service';
import { CreditCard } from '../../../core/models/card.models';
import { StatementAnalyticsComponent } from '../analytics/analytics.component';
import { formatIstDate } from '../../../core/utils/date-time.util';

@Component({
  selector: 'app-statement-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, StatementAnalyticsComponent],
  templateUrl: './statement-detail.component.html'
})
export class StatementDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private statementService = inject(StatementService);
  private dashboardService = inject(DashboardService);

  statement = signal<StatementDetail | null>(null);
  cards = signal<CreditCard[]>([]);
  isLoading = signal(true);
  error = signal<string | null>(null);
  currentPage = signal(1);
  activeTab = signal<'analytics' | 'transactions'>('analytics');
  readonly itemsPerPage = 7;

  ngOnInit(): void {
    const requestedTab = this.route.snapshot.queryParamMap.get('tab');
    if (requestedTab === 'transactions') {
      this.activeTab.set('transactions');
    }

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadStatement(id);
    } else {
      this.error.set('Statement ID not found');
      this.isLoading.set(false);
    }
  }

  loadStatement(id: string): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.currentPage.set(1);

    this.dashboardService.getCards().subscribe({
      next: (cardsRes) => this.cards.set(cardsRes.data || []),
      error: () => this.cards.set([])
    });

    this.fetchStatementDetail(id);
  }

  private fetchStatementDetail(statementId: string): void {
    this.statementService.getStatementById(statementId).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.statement.set(res.data);
          this.error.set(null);
        } else {
          this.statement.set(null);
          this.error.set('Statement not found');
        }
        this.isLoading.set(false);
      },
      error: () => {
        this.statement.set(null);
        this.error.set('Network error loading statement');
        this.isLoading.set(false);
      }
    });
  }

  getStatusLabel(status: number): string {
    switch (status) {
      case 0: return 'Generated';
      case 1: return 'Paid';
      case 2: return 'Overdue';
      case 3: return 'Partially Paid';
      default: return 'Unknown';
    }
  }

  getStatusClass(status: number): string {
    switch (status) {
      case 0: return 'bg-[#ffdcbd]/30 text-[#693c00]';
      case 1: return 'bg-emerald-100 text-emerald-700';
      case 2: return 'bg-red-100 text-red-700';
      case 3: return 'bg-amber-100 text-amber-700';
      default: return 'bg-gray-100 text-gray-700';
    }
  }

  formatDate(date: string | null, includeTime = false): string {
    if (!date) return 'N/A';

    const formatted = includeTime
      ? formatIstDate(date, 'MMM d, y, hh:mm a', 'N/A')
      : formatIstDate(date, 'MMM d, y', 'N/A');

    return includeTime ? `${formatted} IST` : formatted;
  }

  billedAmount(stmt: StatementDetail): number {
    return stmt.totalPurchases + stmt.penaltyCharges + stmt.interestCharges;
  }

  displayCardLast4(stmt: StatementDetail): string {
    if (stmt.cardLast4 && stmt.cardLast4.trim().length > 0) {
      return stmt.cardLast4;
    }

    const card = this.cards().find(c => c.id === stmt.cardId);
    if (card?.last4) {
      return card.last4;
    }

    return '----';
  }

  paginatedTransactions(stmt: StatementDetail) {
    const transactions = stmt.transactions || [];
    const start = (this.currentPage() - 1) * this.itemsPerPage;
    return transactions.slice(start, start + this.itemsPerPage);
  }

  totalTransactionPages(stmt: StatementDetail): number {
    const total = stmt.transactions?.length || 0;
    return Math.max(1, Math.ceil(total / this.itemsPerPage));
  }

  nextPage(stmt: StatementDetail): void {
    if (this.currentPage() < this.totalTransactionPages(stmt)) {
      this.currentPage.set(this.currentPage() + 1);
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.set(this.currentPage() - 1);
    }
  }

  exportToPdf(): void {
    window.print();
  }

  private normalizeStatementTransactionType(type: string | null | undefined): 'purchase' | 'payment' | 'refund' | 'other' {
    const lowered = (type || '').trim().toLowerCase();
    if (lowered === 'purchase' || lowered === 'debit') return 'purchase';
    if (lowered === 'payment' || lowered === 'credit') return 'payment';
    if (lowered === 'refund') return 'refund';
    return 'other';
  }

  isStatementCredit(tx: { type: string; amount: number }): boolean {
    const normalized = this.normalizeStatementTransactionType(tx.type);
    if (normalized === 'payment' || normalized === 'refund') return true;
    if (normalized === 'purchase') return false;
    return tx.amount < 0;
  }

  getStatementTransactionIcon(tx: { type: string; amount: number }): string {
    return this.isStatementCredit(tx) ? 'payments' : 'shopping_cart';
  }

  getStatementTransactionAmountPrefix(tx: { type: string; amount: number }): string {
    return this.isStatementCredit(tx) ? '+' : '-';
  }

  getStatementTransactionAmountValue(tx: { type: string; amount: number }): number {
    return Math.abs(tx.amount);
  }
}