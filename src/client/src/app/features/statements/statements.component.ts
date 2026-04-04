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

    // Keep API order so the latest generated statement stays at the top.
    return filtered;
  });

  cardOptions = computed(() => {
    const statementCardIds = new Set(this.statements().map(s => s.cardId));

    const cardsFromProfile = this.cards()
      .filter(c => statementCardIds.has(c.id))
      .map(c => ({
        id: c.id,
        last4: c.last4,
        issuerName: c.issuerName,
        network: c.network
      }));

    const cardsFromStatements = this.statements()
      .filter(s => !cardsFromProfile.some(c => c.id === s.cardId))
      .reduce((acc, s) => {
        if (!acc.some(c => c.id === s.cardId)) {
          acc.push({
            id: s.cardId,
            last4: s.cardLast4,
            issuerName: s.issuerName,
            network: s.cardNetwork
          });
        }
        return acc;
      }, [] as Array<{ id: string; last4: string; issuerName: string; network: string }>);

    return [...cardsFromProfile, ...cardsFromStatements];
  });

  getCardDisplay(cardId: string): string {
    const card = this.cards().find(c => c.id === cardId);
    if (card) {
      return `${card.issuerName} •••• ${card.last4}`;
    }
    const stmt = this.statements().find(s => s.cardId === cardId);
    if (stmt) {
      return `${stmt.issuerName} •••• ${stmt.cardLast4}`;
    }
    return 'Unknown Card';
  }

  getCardFilterLabel(cardId: string): string {
    const card = this.cardOptions().find(c => c.id === cardId);
    if (!card) return 'All Cards';
    return `${card.issuerName} ${card.network} •••• ${card.last4}`;
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
    return this.getCardFilterLabel(cardId);
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

  getStatementBadgeLabel(statement: Statement): string {
    const status = this.getEffectiveStatementStatus(statement);
    if (status === 2) return 'Paid';
    if (status === 3) return 'Overdue';
    if (status === 4) return 'Partially Paid';
    if (status === 1) return 'Generated';
    return 'Archived';
  }

  getStatementBadgeClass(statement: Statement): string {
    const status = this.getEffectiveStatementStatus(statement);
    if (status === 2) return 'bg-[#ffdcbd]/30 text-[#693c00]';
    if (status === 3) return 'bg-red-500/10 text-red-600';
    if (status === 4) return 'bg-amber-500/10 text-amber-700';
    if (status === 1) return 'bg-slate-500/10 text-slate-700';
    return 'bg-[#e4e2e1] text-[#615e5c]';
  }

  private getEffectiveStatementStatus(statement: Statement): number {
    const closingBalance = Number(statement.closingBalance || 0);
    const amountPaid = Number(statement.amountPaid || 0);
    const rawStatus = Number(statement.status || 1);

    if (closingBalance <= 0) return 2;

    if (statement.dueDateUtc) {
      const dueDate = new Date(statement.dueDateUtc);
      if (dueDate.getTime() < Date.now()) {
        return 3;
      }
    }

    if (amountPaid > 0 || rawStatus === 4) {
      return 4;
    }

    return 1;
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