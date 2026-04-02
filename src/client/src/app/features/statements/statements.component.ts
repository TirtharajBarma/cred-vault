import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { StatementService, Statement } from '../../core/services/rewards.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { CreditCard } from '../../core/models/card.models';

@Component({
  selector: 'app-statements',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './statements.component.html',
  styleUrls: ['./statements.component.css']
})
export class StatementsComponent implements OnInit {
  private statementService = inject(StatementService);
  private dashboardService = inject(DashboardService);
  private router = inject(Router);

  statements = signal<Statement[]>([]);
  cards = signal<CreditCard[]>([]);
  isLoading = signal(true);
  selectedCardFilter = signal<string>('all');
  showFilterDropdown = signal(false);
  currentPage = signal(1);
  itemsPerPage = 7;

  sortedStatements = computed(() => {
    let filtered = this.statements();
    const cardFilter = this.selectedCardFilter();
    if (cardFilter !== 'all') {
      filtered = filtered.filter(s => s.cardId === cardFilter);
    }
    return [...filtered].sort(
      (a, b) => new Date(b.periodEndUtc).getTime() - new Date(a.periodEndUtc).getTime()
    );
  });

  cardOptions = computed(() => {
    const cardMap = new Map<string, { last4: string; issuerName: string; network: string }>();
    this.statements().forEach(s => {
      if (!cardMap.has(s.cardId)) {
        cardMap.set(s.cardId, {
          last4: s.cardLast4,
          issuerName: s.issuerName,
          network: s.cardNetwork
        });
      }
    });
    return Array.from(cardMap.entries()).map(([id, info]) => ({ id, ...info }));
  });

  getCardDisplay(cardId: string): string {
    const card = this.cards().find(c => c.id === cardId);
    if (card) {
      return `${card.issuerName} *${card.last4}`;
    }
    const stmt = this.statements().find(s => s.cardId === cardId);
    if (stmt) {
      return `${stmt.issuerName} *${stmt.cardLast4}`;
    }
    return 'Unknown Card';
  }

  getCardNetworkIcon(network: string): string {
    if (network === 'Visa') return '🔴';
    if (network === 'Mastercard') return '🟠';
    return '💳';
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.dashboardService.getCards().subscribe({
      next: (res) => {
        this.cards.set(res.data || []);
      },
      error: () => this.isLoading.set(false)
    });
    this.statementService.getMyStatements().subscribe({
      next: (res) => {
        this.statements.set(res.data || []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  toggleFilterDropdown(): void {
    this.showFilterDropdown.set(!this.showFilterDropdown());
  }

  selectCardFilter(cardId: string): void {
    this.selectedCardFilter.set(cardId);
    this.showFilterDropdown.set(false);
  }

  getSelectedCardLabel(): string {
    const cardId = this.selectedCardFilter();
    if (cardId === 'all') return 'All Cards';
    const card = this.cards().find(c => c.id === cardId);
    if (card) return `${card.issuerName} *${card.last4}`;
    return 'All Cards';
  }

  paginatedStatements() {
    const start = (this.currentPage() - 1) * this.itemsPerPage;
    return this.sortedStatements().slice(start, start + this.itemsPerPage);
  }

  totalPages(): number {
    return Math.ceil(this.sortedStatements().length / this.itemsPerPage);
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.set(this.currentPage() + 1);
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.set(this.currentPage() - 1);
    }
  }

  getStatementBadgeLabel(status: number): string {
    return status === 2 ? 'Paid' : 'Archived';
  }

  getStatementBadgeClass(status: number): string {
    return status === 2
      ? 'bg-[#ffdcbd]/30 text-[#693c00]'
      : 'bg-[#e4e2e1] text-[#615e5c]';
  }

  getDisplayAmount(statement: Statement): number {
    return statement.closingBalance > 0
      ? statement.closingBalance
      : statement.amountPaid > 0
        ? statement.amountPaid
        : statement.minimumDue;
  }

  viewStatement(id: string): void {
    this.router.navigate(['/statements', id]);
  }
}