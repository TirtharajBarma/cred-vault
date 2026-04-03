import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { CreditCard, CardTransaction, TransactionType } from '../../core/models/card.models';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  cards = signal<CreditCard[]>([]);
  transactions = signal<CardTransaction[]>([]);
  issuers = signal<any[]>([]);
  isLoading = signal(true);
  
  user = this.authService.currentUser;
  
  transactionPage = signal(1);
  itemsPerPage = 7;
  
  showAddCardModal = signal(false);
  addCardForm: FormGroup;
  isSubmitting = signal(false);
  errorMessage = signal<string | null>(null);

  constructor() {
    this.addCardForm = this.fb.group({
      cardholderName: ['', [Validators.required]],
      cardNumber: ['', [Validators.required, Validators.pattern(/^\d{16}$/)]],
      expMonth: [1, [Validators.required, Validators.min(1), Validators.max(12)]],
      expYear: [new Date().getFullYear(), [Validators.required]],
      issuerId: ['', [Validators.required]],
      isDefault: [false]
    });
  }

  ngOnInit(): void {
    this.loadDashboardData();
    this.loadIssuers();
  }

  loadDashboardData(): void {
    this.isLoading.set(true);
    
    const cards$ = this.dashboardService.getCards().pipe(
      catchError(() => of({ success: false, data: [] }))
    );
    
    const transactions$ = this.dashboardService.getAllTransactions().pipe(
      catchError(() => of({ success: false, data: [] }))
    );

    forkJoin({
      cards: cards$,
      transactions: transactions$
    }).subscribe({
      next: (res: any) => {
        if (res.cards.success) {
          this.cards.set(res.cards.data || []);
        }
        if (res.transactions.success) {
          this.transactions.set(res.transactions.data || []);
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('[Dashboard] Unexpected error:', err);
        this.isLoading.set(false);
      }
    });
  }

  loadIssuers(): void {
    this.dashboardService.getIssuers().subscribe(res => {
      if (res.success) this.issuers.set(res.data || []);
    });
  }

  toggleAddCardModal(): void {
    this.showAddCardModal.set(!this.showAddCardModal());
    this.errorMessage.set(null);
    if (!this.showAddCardModal()) {
      this.addCardForm.reset({ expMonth: 1, expYear: new Date().getFullYear(), isDefault: false });
    }
  }

  onSubmitCard(): void {
    if (this.addCardForm.invalid) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    
    this.dashboardService.addCard(this.addCardForm.value).subscribe({
      next: (res) => {
        if (res.success) {
          this.loadDashboardData();
          this.toggleAddCardModal();
        } else {
          this.errorMessage.set(res.message || 'Failed to add card.');
        }
        this.isSubmitting.set(false);
      },
      error: (err) => {
        const msg = err?.error?.message || 'A server error occurred. Please try again.';
        this.errorMessage.set(msg);
        this.isSubmitting.set(false);
      }
    });
  }

  getTransactionIcon(type: TransactionType): string {
    switch (this.getNormalizedTransactionType(type)) {
      case TransactionType.Purchase: return 'shopping_cart';
      case TransactionType.Payment: return 'payments';
      case TransactionType.Refund: return 'settings_backup_restore';
      default: return 'receipt';
    }
  }

  getNormalizedTransactionType(type: TransactionType | string | number): TransactionType {
    if (type === TransactionType.Purchase || type === 1 || type === '1' || type === 'Purchase') return TransactionType.Purchase;
    if (type === TransactionType.Payment || type === 2 || type === '2' || type === 'Payment') return TransactionType.Payment;
    if (type === TransactionType.Refund || type === 3 || type === '3' || type === 'Refund') return TransactionType.Refund;
    return TransactionType.Purchase;
  }

  getTransactionTitle(tx: CardTransaction): string {
    const description = (tx.description || '').trim();
    const lowered = description.toLowerCase();

    if (lowered.startsWith('bill payment:')) {
      return `Bill Statement of ${this.getMonthYear(tx.dateUtc)}`;
    }

    if (lowered.startsWith('saga:') && this.getNormalizedTransactionType(tx.type) === TransactionType.Payment) {
      return `Bill Statement of ${this.getMonthYear(tx.dateUtc)}`;
    }

    return description || 'Card Transaction';
  }

  getTransactionCardMeta(tx: CardTransaction): string {
    const card = this.cards().find(c => c.id === tx.cardId);
    if (!card) return 'Card details unavailable';

    const network = card.network || 'Card';
    const issuer = card.issuerName || 'CredVault';
    return `${issuer} • ${network} •••• ${card.last4}`;
  }

  getTransactionFlowLabel(type: TransactionType | string | number): string {
    return this.getNormalizedTransactionType(type) === TransactionType.Purchase ? 'Debit' : 'Credit';
  }

  getTransactionTypeLabel(type: TransactionType | string | number): string {
    const normalized = this.getNormalizedTransactionType(type);
    if (normalized === TransactionType.Purchase) return 'Purchase';
    if (normalized === TransactionType.Payment) return 'Payment';
    return 'Refund';
  }

  getSignedAmountPrefix(type: TransactionType | string | number): string {
    return this.getNormalizedTransactionType(type) === TransactionType.Purchase ? '-' : '+';
  }

  private getMonthYear(dateUtc: string): string {
    return new Intl.DateTimeFormat('en-IN', {
      month: 'short',
      year: 'numeric'
    }).format(new Date(dateUtc));
  }

  getCardGradient(index: number): string {
    const gradients = ['card-gradient-1', 'card-gradient-2', 'card-gradient-3', 'card-gradient-4', 'card-gradient-5', 'card-gradient-6', 'card-gradient-7'];
    return gradients[index % gradients.length];
  }

  getCardStyle(index: number): string {
    const gradients = [
      'background: linear-gradient(135deg, #1e1b13 0%, #4a4228 100%)',
      'background: linear-gradient(135deg, #eebd2b 0%, #8c6d1a 100%)',
      'background: linear-gradient(135deg, #2d2d2d 0%, #1a1a1a 100%)',
      'background: linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
      'background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%)',
      'background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)',
      'background: linear-gradient(135deg, #fa709a 0%, #fee140 100%)'
    ];
    const shadows = [
      'box-shadow: 0 20px 50px -10px rgba(30, 27, 19, 0.5)',
      'box-shadow: 0 20px 50px -10px rgba(238, 189, 43, 0.4)',
      'box-shadow: 0 20px 50px -10px rgba(45, 45, 45, 0.5)',
      'box-shadow: 0 20px 50px -10px rgba(102, 126, 234, 0.4)',
      'box-shadow: 0 20px 50px -10px rgba(240, 147, 251, 0.4)',
      'box-shadow: 0 20px 50px -10px rgba(79, 172, 254, 0.4)',
      'box-shadow: 0 20px 50px -10px rgba(250, 112, 154, 0.4)'
    ];
    return gradients[index % gradients.length] + '; ' + shadows[index % shadows.length];
  }

  paginatedTransactions() {
    const sorted = [...this.transactions()].sort(
      (a, b) => new Date(b.dateUtc).getTime() - new Date(a.dateUtc).getTime()
    );
    const start = (this.transactionPage() - 1) * this.itemsPerPage;
    return sorted.slice(start, start + this.itemsPerPage);
  }

  totalTransactionPages(): number {
    return Math.ceil(this.transactions().length / this.itemsPerPage);
  }

  nextTransactionPage(): void {
    if (this.transactionPage() < this.totalTransactionPages()) {
      this.transactionPage.set(this.transactionPage() + 1);
    }
  }

  prevTransactionPage(): void {
    if (this.transactionPage() > 1) {
      this.transactionPage.set(this.transactionPage() - 1);
    }
  }

  getIssuerLabel(issuer: any): string {
    const raw = issuer?.network;
    const network = typeof raw === 'string'
      ? raw
      : raw === 1
        ? 'Visa'
        : raw === 2
          ? 'Mastercard'
          : 'Unknown';

    return `${issuer?.name ?? 'Issuer'} (${network})`;
  }

  getNetworkLogo(network: string | null | undefined): string | null {
    if (network === 'Visa') return '/assets/visa.png';
    if (network === 'Mastercard') return '/assets/mastercard.png';
    return null;
  }

  isCardConfigured(card: CreditCard): boolean {
    return card.isDeleted !== true && card.creditLimit > 0;
  }

  getTotalAssets(): number {
    return this.cards().reduce((acc, card) => acc + card.creditLimit, 0);
  }

  showDeleteConfirm = signal(false);
  deleteCardId = signal<string | null>(null);
  deleteCardName = signal<string | null>(null);

  confirmDeleteCard(cardId: string, cardName: string, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    this.deleteCardId.set(cardId);
    this.deleteCardName.set(cardName);
    this.showDeleteConfirm.set(true);
  }

  cancelDelete(): void {
    this.showDeleteConfirm.set(false);
    this.deleteCardId.set(null);
    this.deleteCardName.set(null);
  }

  executeDeleteCard(): void {
    const cardId = this.deleteCardId();
    if (!cardId) return;

    this.dashboardService.deleteCard(cardId).subscribe({
      next: () => {
        this.loadDashboardData();
        this.cancelDelete();
      },
      error: (err) => {
        console.error('[Dashboard] Delete card failed:', err);
        this.cancelDelete();
      }
    });
  }

  showNotConfiguredModal = signal(false);
  notConfiguredCard = signal<CreditCard | null>(null);

  openCardNotConfigured(event: Event): void {
    event.stopPropagation();
    event.preventDefault();
  }
}
