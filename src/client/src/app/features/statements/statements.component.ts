import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { StatementService, Statement } from '../../core/services/rewards.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { CreditCard } from '../../core/models/card.models';
import { IstDatePipe } from '../../shared/pipes/ist-date.pipe';
import { formatIstDate, parseUtcDate } from '../../core/utils/date-time.util';

@Component({
  selector: 'app-statements',
  standalone: true,
  imports: [CommonModule, IstDatePipe],
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
  selectedMonthFilter = signal<string>('all');
  showFilterDropdown = signal(false);
  showMonthDropdown = signal(false);
  currentPage = signal(1);
  itemsPerPage = 7;

  sortedStatements = computed(() => {
    const visibleCardIds = new Set(this.cardOptions().map(card => card.id));
    let filtered = this.statements().filter(statement => visibleCardIds.has(statement.cardId));
    const cardFilter = this.selectedCardFilter();
    const monthFilter = this.selectedMonthFilter();
    
    if (cardFilter !== 'all') {
      filtered = filtered.filter(s => s.cardId === cardFilter);
    }
    if (monthFilter !== 'all') {
      filtered = filtered.filter(s => s.statementPeriod === monthFilter);
    }

    // Keep API order so the latest generated statement stays at the top.
    return filtered;
  });

  monthOptions = computed(() => {
    const months = new Set(this.statements().map(s => s.statementPeriod));
    // Simple sort for 'Jan 2024' formats assuming they are roughly consistent, or just rely on API order
    return Array.from(months);
  });

  cardOptions = computed(() => {
    const statementCardIds = new Set(this.statements().map(s => s.cardId));

    return this.cards()
      .filter(c => statementCardIds.has(c.id))
      .map(c => ({
        id: c.id,
        last4: c.last4,
        issuerName: c.issuerName,
        network: c.network
      }));
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
        this.syncSelectedCardFilter();
      },
      error: () => this.isLoading.set(false)
    });
    this.statementService.getMyStatements().subscribe({
      next: (res) => {
        this.statements.set(res.data || []);
        this.syncSelectedCardFilter();
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  private syncSelectedCardFilter(): void {
    const selected = this.selectedCardFilter();
    if (selected === 'all') return;

    const isVisibleOption = this.cardOptions().some(card => card.id === selected);
    if (!isVisibleOption) {
      this.selectedCardFilter.set('all');
    }
  }

  toggleFilterDropdown(): void {
    this.showFilterDropdown.set(!this.showFilterDropdown());
    this.showMonthDropdown.set(false);
  }

  toggleMonthDropdown(): void {
    this.showMonthDropdown.set(!this.showMonthDropdown());
    this.showFilterDropdown.set(false);
  }

  selectCardFilter(cardId: string): void {
    this.selectedCardFilter.set(cardId);
    this.showFilterDropdown.set(false);
  }

  selectMonthFilter(month: string): void {
    this.selectedMonthFilter.set(month);
    this.showMonthDropdown.set(false);
  }

  exportAllToCSV(): void {
    const stmts = this.sortedStatements();
    if (stmts.length === 0) return;
    
    const headers = ['Statement ID', 'Issuer', 'Network', 'Card Last 4', 'Period', 'Status', 'Total Amount', 'Min Due', 'Due Date'];
    const rows = stmts.map(s => {
      const card = this.cards().find(c => c.id === s.cardId);
      const issuer = card?.issuerName || s.issuerName || 'Unknown Issuer';
      const last4 = card?.last4 || s.cardLast4 || 'N/A';
      const network = card?.network || s.cardNetwork || 'Unknown';

      return [
        s.id,
        `"${issuer}"`,
        network,
        last4,
        `"${s.statementPeriod}"`,
        this.getStatementBadgeLabel(s),
        this.getDisplayAmount(s),
        s.minimumDue,
        s.dueDateUtc ? formatIstDate(s.dueDateUtc, 'MM/dd/yyyy', 'N/A') : 'N/A'
      ];
    });
    
    const csvContent = [
      headers.join(','),
      ...rows.map(r => r.join(','))
    ].join('\n');
    
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    link.setAttribute('href', url);
    link.setAttribute('download', `statements_export_${new Date().toISOString().split('T')[0]}.csv`);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
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
      const dueDate = parseUtcDate(statement.dueDateUtc);
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