import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { StatementService, StatementDetail } from '../../../core/services/rewards.service';
import { DashboardService } from '../../../core/services/dashboard.service';
import { CreditCard } from '../../../core/models/card.models';

@Component({
  selector: 'app-statement-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
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
  readonly itemsPerPage = 7;

  ngOnInit(): void {
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

    this.statementService.getStatementByBillId(id).subscribe({
      next: (billRes) => {
        const billLookupData = billRes.data;
        const statementFromBill = Array.isArray(billLookupData)
          ? billLookupData[0]
          : billLookupData;

        if (billRes.success && statementFromBill?.id) {
          this.fetchStatementDetail(statementFromBill.id);
          return;
        }

        this.fetchStatementDetail(id);
      },
      error: () => {
        this.fetchStatementDetail(id);
      }
    });
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
      case 1: return 'Pending';
      case 2: return 'Paid';
      case 3: return 'Overdue';
      default: return 'Unknown';
    }
  }

  getStatusClass(status: number): string {
    switch (status) {
      case 1: return 'bg-[#ffdcbd]/30 text-[#693c00]';
      case 2: return 'bg-emerald-100 text-emerald-700';
      case 3: return 'bg-red-100 text-red-700';
      default: return 'bg-gray-100 text-gray-700';
    }
  }

  formatDate(date: string | null, includeTime = false): string {
    if (!date) return 'N/A';

    const options: Intl.DateTimeFormatOptions = includeTime
      ? {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
          hour: '2-digit',
          minute: '2-digit',
          hour12: true,
          timeZone: 'Asia/Kolkata'
        }
      : {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
          timeZone: 'Asia/Kolkata'
        };

    const formatted = new Intl.DateTimeFormat('en-IN', options).format(new Date(date));
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
}