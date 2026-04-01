import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatementService, Statement, StatementDetail } from '../../core/services/rewards.service';
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

  statements = signal<Statement[]>([]);
  cards = signal<CreditCard[]>([]);
  isLoading = signal(true);
  selectedStatement = signal<StatementDetail | null>(null);
  showDetailModal = signal(false);
  isGenerating = signal(false);
  generateCardId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.dashboardService.getCards().subscribe({
      next: (res) => {
        this.cards.set(res.data || []);
        this.statementService.getMyStatements().subscribe({
          next: (res) => {
            this.statements.set(res.data || []);
            this.isLoading.set(false);
          },
          error: () => this.isLoading.set(false)
        });
      },
      error: () => this.isLoading.set(false)
    });
  }

  getCardName(cardId: string): string {
    const card = this.cards().find(c => c.id === cardId);
    if (!card) return 'Unknown Card';
    return `${card.issuerName} ${card.network} *${card.last4}`;
  }

  getStatusLabel(status: number): string {
    const labels: Record<number, string> = { 1: 'Generated', 2: 'Paid', 3: 'Overdue', 4: 'Partially Paid' };
    return labels[status] || 'Unknown';
  }

  getStatusClass(status: number): string {
    const classes: Record<number, string> = {
      1: 'bg-blue-100 text-blue-700',
      2: 'bg-green-100 text-green-700',
      3: 'bg-red-100 text-red-700',
      4: 'bg-amber-100 text-amber-700'
    };
    return classes[status] || 'bg-slate-100 text-slate-700';
  }

  viewStatement(id: string): void {
    this.statementService.getStatementById(id).subscribe({
      next: (res) => {
        if (res.data) {
          this.selectedStatement.set(res.data);
          this.showDetailModal.set(true);
        }
      }
    });
  }

  closeDetail(): void {
    this.showDetailModal.set(false);
    this.selectedStatement.set(null);
  }

  generateStatement(cardId: string): void {
    this.isGenerating.set(true);
    this.generateCardId.set(cardId);
    this.statementService.generateStatement(cardId).subscribe({
      next: () => {
        this.isGenerating.set(false);
        this.loadData();
      },
      error: () => this.isGenerating.set(false)
    });
  }

  getTxnTypeClass(type: string): string {
    switch (type) {
      case 'Purchase': return 'text-red-600';
      case 'Payment': return 'text-green-600';
      case 'Refund': return 'text-blue-600';
      default: return 'text-slate-600';
    }
  }

  getTxnSign(type: string): string {
    switch (type) {
      case 'Purchase': return '+';
      case 'Payment': return '-';
      case 'Refund': return '-';
      default: return '';
    }
  }
}
