import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardService } from '../../core/services/dashboard.service';
import { AdminService } from '../../core/services/admin.service';
import { AuthService } from '../../core/services/auth.service';
import { CreditCard, CardTransaction, TransactionType } from '../../core/models/card.models';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { RouterLink } from '@angular/router';
import { IstDatePipe } from '../../shared/pipes/ist-date.pipe';
import { formatIstDate, getUtcTimestamp } from '../../core/utils/date-time.util';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, IstDatePipe],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private adminService = inject(AdminService);
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
  addCardStep = signal<1 | 2>(1);
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
    this.adminService.getIssuers().subscribe(res => {
      if (res.success) this.issuers.set(res.data || []);
    });
  }

  toggleAddCardModal(): void {
    this.showAddCardModal.set(!this.showAddCardModal());
    this.errorMessage.set(null);
    this.addCardStep.set(1);
    if (!this.showAddCardModal()) {
      this.addCardForm.reset({ expMonth: 1, expYear: new Date().getFullYear(), isDefault: false });
    }
  }

  getAddCardProgressWidth(): string {
    const step = this.addCardStep();
    if (step === 1) return '50%';
    return '100%';
  }

  goToCardDetailsStep(): void {
    this.errorMessage.set(null);
    this.addCardStep.set(1);
  }

  goToReviewStep(): void {
    this.errorMessage.set(null);

    const cardholderNameCtrl = this.addCardForm.get('cardholderName');
    const cardNumberCtrl = this.addCardForm.get('cardNumber');
    const expMonthCtrl = this.addCardForm.get('expMonth');
    const expYearCtrl = this.addCardForm.get('expYear');
    const issuerIdCtrl = this.addCardForm.get('issuerId');

    cardholderNameCtrl?.markAsTouched();
    cardNumberCtrl?.markAsTouched();
    expMonthCtrl?.markAsTouched();
    expYearCtrl?.markAsTouched();
    issuerIdCtrl?.markAsTouched();

    const normalizedCardNumber = this.normalizeCardNumber(String(cardNumberCtrl?.value ?? ''));
    const currentYear = new Date().getFullYear();
    const expYear = Number(expYearCtrl?.value ?? 0);

    if (!cardholderNameCtrl?.value || !issuerIdCtrl?.value) {
      this.errorMessage.set('Please fill all required card details before continuing.');
      return;
    }

    if (normalizedCardNumber.length !== 16) {
      this.errorMessage.set('Card number must contain exactly 16 digits.');
      return;
    }

    const selectedIssuerId = String(issuerIdCtrl?.value ?? '');
    const networkValidation = this.validateCardNetworkForIssuer(normalizedCardNumber, selectedIssuerId);
    if (!networkValidation.valid) {
      this.errorMessage.set(networkValidation.message);
      return;
    }

    if (expYear < currentYear) {
      this.errorMessage.set('Expiry year cannot be in the past.');
      return;
    }

    cardNumberCtrl?.setValue(normalizedCardNumber);
    this.addCardStep.set(2);
  }

  formatCardNumberForReview(): string {
    const digits = this.normalizeCardNumber(String(this.addCardForm.value.cardNumber || ''));
    if (!digits) return '---- ---- ---- ----';
    return digits.match(/.{1,4}/g)?.join(' ') || digits;
  }

  getReviewIssuerLabel(): string {
    const issuerId = this.addCardForm.value.issuerId;
    if (!issuerId) return 'Not selected';

    const issuer = this.issuers().find(item => item.id === issuerId);
    if (!issuer) return 'Not selected';

    return this.getIssuerLabel(issuer);
  }

  getMaskedCardNumberForReview(): string {
    const digits = this.normalizeCardNumber(String(this.addCardForm.value.cardNumber || ''));
    if (digits.length !== 16) return '•••• •••• •••• ••••';

    return `•••• •••• •••• ${digits.slice(-4)}`;
  }

  onSubmitCard(): void {
    if (this.addCardStep() !== 2) {
      this.goToReviewStep();
      return;
    }

    if (this.addCardForm.invalid) return;

    const normalizedCardNumber = this.normalizeCardNumber(String(this.addCardForm.value.cardNumber || ''));
    if (normalizedCardNumber.length !== 16) {
      this.errorMessage.set('Card number must contain exactly 16 digits.');
      return;
    }

    const selectedIssuerId = String(this.addCardForm.value.issuerId || '');
    const networkValidation = this.validateCardNetworkForIssuer(normalizedCardNumber, selectedIssuerId);
    if (!networkValidation.valid) {
      this.errorMessage.set(networkValidation.message);
      return;
    }

    const payload = {
      ...this.addCardForm.value,
      cardNumber: normalizedCardNumber
    };

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    
    this.dashboardService.addCard(payload).subscribe({
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

  private normalizeCardNumber(value: string): string {
    return (value || '').replace(/\D/g, '');
  }

  private getDetectedCardNetwork(cardNumber: string): 'Visa' | 'Mastercard' | 'Unknown' {
    if (!/^\d{16}$/.test(cardNumber)) return 'Unknown';

    if (cardNumber.startsWith('4')) return 'Visa';

    const firstTwo = Number(cardNumber.slice(0, 2));
    const firstFour = Number(cardNumber.slice(0, 4));
    if ((firstTwo >= 51 && firstTwo <= 55) || (firstFour >= 2221 && firstFour <= 2720)) {
      return 'Mastercard';
    }

    return 'Unknown';
  }

  private getIssuerNetwork(issuerId: string): 'Visa' | 'Mastercard' | 'Unknown' {
    const issuer = this.issuers().find(item => item.id === issuerId);
    const raw = issuer?.network;

    if (raw === 'Visa' || raw === 1 || raw === '1') return 'Visa';
    if (raw === 'Mastercard' || raw === 2 || raw === '2') return 'Mastercard';

    return 'Unknown';
  }

  private validateCardNetworkForIssuer(
    cardNumber: string,
    issuerId: string
  ): { valid: boolean; message: string } {
    const detected = this.getDetectedCardNetwork(cardNumber);
    if (detected === 'Unknown') {
      return { valid: false, message: 'Enter a valid Visa or Mastercard card number.' };
    }

    const issuerNetwork = this.getIssuerNetwork(issuerId);
    if (issuerNetwork === 'Unknown') {
      return { valid: false, message: 'Selected issuer network is invalid. Please choose another issuer.' };
    }

    if (detected !== issuerNetwork) {
      return {
        valid: false,
        message: `Card number looks like ${detected}, but selected issuer is ${issuerNetwork}. Please choose a matching issuer.`
      };
    }

    return { valid: true, message: '' };
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
    if (type === 0 || type === '0' || type === 'Purchase') return 0;
    if (type === 1 || type === '1' || type === 'Payment') return 1;
    if (type === 2 || type === '2' || type === 'Refund') return 2;
    return 0;
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
    return this.getNormalizedTransactionType(type) === 0 ? 'Debit' : 'Credit';
  }

  getTransactionTypeLabel(type: TransactionType | string | number): string {
    const normalized = this.getNormalizedTransactionType(type);
    if (normalized === TransactionType.Purchase) return 'Purchase';
    if (normalized === TransactionType.Payment) return 'Payment';
    return 'Refund';
  }

  getSignedAmountPrefix(type: TransactionType | string | number): string {
    return this.getNormalizedTransactionType(type) === 0 ? '-' : '+';
  }

  private getMonthYear(dateUtc: string): string {
    return formatIstDate(dateUtc, 'MMM yyyy', '-');
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
      (a, b) => getUtcTimestamp(b.dateUtc) - getUtcTimestamp(a.dateUtc)
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
  deleteErrorMessage = signal<string | null>(null);
  deleteBlockedReason = signal<string | null>(null);

  hasOutstandingBalance(cardId: string | null): boolean {
    if (!cardId) return false;
    const card = this.cards().find(item => item.id === cardId);
    if (!card) return false;

    return Number(card.outstandingBalance) > 0;
  }

  confirmDeleteCard(cardId: string, cardName: string, event: Event): void {
    event.stopPropagation();
    event.preventDefault();

    this.deleteErrorMessage.set(null);
    this.deleteCardId.set(cardId);
    this.deleteCardName.set(cardName);

    if (this.hasOutstandingBalance(cardId)) {
      this.deleteBlockedReason.set('Cannot delete card with outstanding balance. Pay your bill first.');
    } else {
      this.deleteBlockedReason.set(null);
    }

    this.showDeleteConfirm.set(true);
  }

  cancelDelete(): void {
    this.showDeleteConfirm.set(false);
    this.deleteCardId.set(null);
    this.deleteCardName.set(null);
    this.deleteErrorMessage.set(null);
    this.deleteBlockedReason.set(null);
  }

  executeDeleteCard(): void {
    const cardId = this.deleteCardId();
    if (!cardId) return;

    if (this.deleteBlockedReason()) {
      return;
    }

    this.dashboardService.deleteCard(cardId).subscribe({
      next: () => {
        this.loadDashboardData();
        this.cancelDelete();
      },
      error: (err) => {
        console.error('[Dashboard] Delete card failed:', err);

        const backendMessage = err?.error?.message;
        this.deleteErrorMessage.set(backendMessage || 'Unable to delete this card right now. Please try again.');
      }
    });
  }
}
