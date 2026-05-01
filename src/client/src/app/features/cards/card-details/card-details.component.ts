import { Component, OnDestroy, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DashboardService } from '../../../core/services/dashboard.service';
import { RewardsService } from '../../../core/services/rewards.service';
import { BillingService, Bill, BillStatus } from '../../../core/services/billing.service';
import { PaymentService, PaymentInitiateRequest, PaymentInitiateResponse } from '../../../core/services/payment.service';
import { CreditCard, CardTransaction } from '../../../core/models/card.models';
import { ApiResponse } from '../../../core/models/auth.models';
import { environment } from '../../../../environments/environment';
import { finalize } from 'rxjs';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { formatIstDate, getUtcTimestamp, parseUtcDate } from '../../../core/utils/date-time.util';

@Component({
  selector: 'app-card-details',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, IstDatePipe],
  templateUrl: './card-details.component.html',
  styleUrl: './card-details.component.css'
})
export class CardDetailsComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private dashboardService = inject(DashboardService);
  private rewardsService = inject(RewardsService);
  private billingService = inject(BillingService);
  private paymentService = inject(PaymentService);

  // Bill Status enum for template access
  billStatus = BillStatus;

  card = signal<CreditCard | null>(null);
  transactions = signal<CardTransaction[]>([]);
  rewards = signal<number>(0);
  isLoading = signal(true);
  error = signal<string | null>(null);
  isFullCardNumberVisible = signal(false);
  isRevealingCardNumber = signal(false);
  fullCardNumber = signal<string | null>(null);
  revealCardNumberError = signal<string | null>(null);

  displayCardNumber = computed(() => {
    const c = this.card();
    if (!c) return '';

    if (this.isFullCardNumberVisible() && this.fullCardNumber()) {
      return this.formatCardNumber(this.fullCardNumber()!);
    }

    return `•••• •••• •••• ${c.last4}`;
  });

  // Bill-related signals
  currentBill = signal<Bill | null>(null);
  isLoadingBill = signal(false);

  // Payment-related signals
  showPaymentModal = signal(false);
  showOtpModal = signal(false);
  showSuccessModal = signal(false);
  paymentType = signal<'full' | 'min'>('full');
  paymentStage = signal<'initiated' | 'otp_sent' | 'processing' | 'completed'>('initiated');
  isProcessingPayment = signal(false);
  paymentError = signal<string | null>(null);
  currentPaymentId = signal<string | null>(null);
  otpCode = '';
  isVerifyingOtp = signal(false);
  otpError = signal<string | null>(null);
  canResendOtp = signal(true);
  paidAmount = signal(0);
  useRewards = signal(false);
  readonly pointsToRupeeRate = environment.pointsToRupeeRate;

  private paymentStatusPollTimer: ReturnType<typeof setInterval> | null = null;
  private paymentStatusPollAttempts = 0;
  private readonly maxPaymentStatusPollAttempts = 20;
  private readonly paymentStatusPollIntervalMs = 2000;

  availableRedeemablePoints = computed(() => Math.floor(this.availablePoints()));
  availablePoints = computed(() => this.rewards());
  rewardValue = computed(() => this.availableRedeemablePoints() * this.pointsToRupeeRate);

  maxRedeemablePoints = computed(() => {
    const bill = this.currentBill();
    if (!bill) return 0;

    const selectedPaymentAmount = this.getPaymentAmount(bill, this.paymentType());
    const maxPointsByAmount = Math.floor(selectedPaymentAmount / this.pointsToRupeeRate);
    return Math.min(this.availableRedeemablePoints(), maxPointsByAmount);
  });

  // Pagination
  currentPage = signal(1);
  itemsPerPage = 7;

  // Form Signals
  isSubmitting = signal(false);
  submitSuccess = signal(false);
  submitError = signal<string | null>(null);
  txAmount = signal<number | null>(null);
  txDescription = signal<string>('');

  // Real-time Health Score based on actual Backend Utilization Ratio
  healthScore = computed(() => {
    const c = this.card();
    if (!c || c.creditLimit <= 0) return 750;
    
    const utilization = c.outstandingBalance / c.creditLimit;
    // Score logic: 1000 - (utilization * 1000), floor at 300
    return Math.round(Math.max(300, 1000 - (utilization * 1000)));
  });

  healthLabel = computed(() => {
    const score = this.healthScore();
    if (score > 800) return 'Excellent';
    if (score > 700) return 'Good';
    if (score > 500) return 'Fair';
    return 'Poor';
  });

  // Dynamic color for health score text
  healthColor = computed(() => {
    const score = this.healthScore();
    if (score > 800) return 'text-emerald-500';
    if (score > 700) return 'text-primary'; // Amber-gold for Good
    if (score > 500) return 'text-amber-500';
    return 'text-red-500';
  });

  // Real-time Spending Analysis based on Category breakdown
  spendingAnalysis = computed(() => {
    const card = this.card();
    if (!card) return [];

    const txs = this.currentCardTransactions();
    if (txs.length === 0) return [];

    // Get current month's transactions only (resets when new bill is generated)
    const now = new Date();
    const currentMonth = now.getMonth();
    const currentYear = now.getFullYear();
    const monthTxs = txs.filter(t => {
      const d = parseUtcDate(t.dateUtc);
      if (Number.isNaN(d.getTime())) return false;
      return d.getMonth() === currentMonth && d.getFullYear() === currentYear;
    });

    if (monthTxs.length === 0) return [];

    const categories: Record<string, number> = {};
    let total = 0;

    for (const tx of monthTxs) {
      const normalizedType = this.getNormalizedTransactionType(tx.type);

      if (normalizedType === 0) {
        const desc = (tx.description || '').toLowerCase();
        let category = 'Services';

        if (desc.includes('food') || desc.includes('rest') || desc.includes('dine') || desc.includes('cafe')) category = 'Dining';
        else if (desc.includes('shop') || desc.includes('amazon') || desc.includes('store') || desc.includes('market') || desc.includes('mall')) category = 'Shopping';
        else if (desc.includes('travel') || desc.includes('uber') || desc.includes('flight') || desc.includes('hotel') || desc.includes('lyft')) category = 'Travel';
        else if (desc.includes('rent') || desc.includes('mortgage') || desc.includes('lease') || desc.includes('housing')) category = 'Housing';
        else if (desc.includes('electricity') || desc.includes('water') || desc.includes('internet') || desc.includes('bill') || desc.includes('utility')) category = 'Utilities';
        else if (desc.includes('netflix') || desc.includes('spotify') || desc.includes('game') || desc.includes('movie') || desc.includes('hulu')) category = 'Entertainment';

        categories[category] = (categories[category] || 0) + tx.amount;
        total += tx.amount;
      }
    }

    if (total <= 0) return [];

    return Object.entries(categories)
      .map(([name, amount]) => ({
        name,
        amount,
        percentage: Math.round((amount / total) * 100)
      }))
      .sort((a, b) => b.amount - a.amount);
  });

  currentCardTransactions = computed(() => {
    const cardId = this.card()?.id;
    if (!cardId) return [];
    return this.sortedTransactions().filter(t => t.cardId === cardId);
  });

  sortedTransactions = computed(() => {
    return [...this.transactions()].sort(
      (a, b) => getUtcTimestamp(b.dateUtc) - getUtcTimestamp(a.dateUtc)
    );
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const cardId = params.get('id');
      if (cardId) {
        this.clearCardNumberRevealState();
        this.loadCardDetails(cardId);
        this.loadRewards();
        this.loadBillForCard(cardId);
      } else {
        this.error.set('Card ID not found');
        this.isLoading.set(false);
      }
    });
  }

  ngOnDestroy(): void {
    this.stopPaymentStatusPolling();
    this.clearCardNumberRevealState();
  }

  loadBillForCard(cardId: string): void {
    this.isLoadingBill.set(true);
    this.billingService.getMyBills().subscribe({
      next: (res: ApiResponse<Bill[]>) => {
        if (res.success && res.data) {
          // Find bill for this card that is pending/overdue/partially paid
          const cardBill = res.data.find(b => 
            b.cardId === cardId && 
            (b.status === BillStatus.Pending || 
             b.status === BillStatus.Overdue || 
             b.status === BillStatus.PartiallyPaid)
          );
          this.currentBill.set(cardBill || null);
        }
        this.isLoadingBill.set(false);
      },
      error: () => {
        this.isLoadingBill.set(false);
      }
    });
  }

  loadCardDetails(cardId: string): void {
    this.isLoading.set(true);
    
    this.dashboardService.getCardById(cardId).subscribe({
      next: (res: ApiResponse<CreditCard>) => {
        if (res.success && res.data) {
          this.card.set(res.data);
        } else {
          this.error.set(res.message || 'Failed to load card details');
        }
      },
      error: (err: any) => this.error.set('Network error while loading card details')
    });

    this.dashboardService.getTransactionsByCardId(cardId).pipe(
      finalize(() => this.isLoading.set(false))
    ).subscribe({
      next: (res: ApiResponse<CardTransaction[]>) => {
        if (res.success && res.data) {
          this.transactions.set(res.data);
        }
      },
      error: (err: any) => console.error('Error loading transactions', err)
    });
  }

  loadRewards(): void {
    this.rewardsService.getRewardAccount().subscribe({
      next: (res: ApiResponse<any>) => {
        if (res.success && res.data) {
          this.rewards.set(res.data.pointsBalance || 0);
        }
      }
    });
  }

  submitTransaction(): void {
    const c = this.card();
    const amount = this.txAmount();
    const desc = this.txDescription();

    if (!c || !amount || !desc) {
      this.submitError.set('Please fill all fields');
      return;
    }

    this.isSubmitting.set(true);
    this.submitError.set(null);
    this.submitSuccess.set(false);

    this.dashboardService.addTransaction(c.id, {
      type: 0, // Purchase
      amount: amount,
      description: desc,
      dateUtc: new Date().toISOString()
    }).pipe(
      finalize(() => this.isSubmitting.set(false))
    ).subscribe({
      next: (res: ApiResponse<any>) => {
        if (res.success) {
          this.submitSuccess.set(true);
          this.txAmount.set(null);
          this.txDescription.set('');
          
          this.loadCardDetails(c.id);
          this.loadRewards();

          setTimeout(() => this.submitSuccess.set(false), 3000);
        } else {
          this.submitError.set(res.message || 'Transaction failed');
        }
      },
      error: (err: any) => {
        const backendMessage = err?.error?.message || err?.error?.data?.message || err?.message;
        this.submitError.set(backendMessage || 'Transaction could not be processed. Please try again.');
      }
    });
  }

  getCardGradient(index: number): string {
    const gradients = ['card-gradient-1', 'card-gradient-2', 'card-gradient-3'];
    return gradients[index % gradients.length];
  }

  getNetworkLogo(network: string | null | undefined): string | null {
    if (network === 'Visa') return '/assets/visa.png';
    if (network === 'Mastercard') return '/assets/mastercard.png';
    return null;
  }

  toggleCardNumberVisibility(): void {
    const card = this.card();
    if (!card || this.isRevealingCardNumber()) {
      return;
    }

    if (this.isFullCardNumberVisible()) {
      this.clearCardNumberRevealState();
      return;
    }

    this.isRevealingCardNumber.set(true);
    this.revealCardNumberError.set(null);

    this.dashboardService.getFullCardNumber(card.id).pipe(
      finalize(() => this.isRevealingCardNumber.set(false))
    ).subscribe({
      next: (res) => {
        if (res.success && res.data?.cardNumber) {
          this.fullCardNumber.set(res.data.cardNumber);
          this.isFullCardNumberVisible.set(true);
          return;
        }

        const message = (res.message || '').toLowerCase();
        if (message.includes('not available')) {
          this.revealCardNumberError.set('Full number is unavailable for this older card. Re-add card to enable reveal.');
        } else {
          this.revealCardNumberError.set('Could not reveal card number right now. Please try again.');
        }

        setTimeout(() => this.revealCardNumberError.set(null), 3500);
      },
      error: () => {
        this.revealCardNumberError.set('Could not reveal card number right now. Please try again.');
        setTimeout(() => this.revealCardNumberError.set(null), 3500);
      }
    });
  }

  private clearCardNumberRevealState(): void {
    this.isFullCardNumberVisible.set(false);
    this.fullCardNumber.set(null);
    this.revealCardNumberError.set(null);
    this.isRevealingCardNumber.set(false);
  }

  private formatCardNumber(value: string): string {
    const digits = (value || '').replace(/\D/g, '');
    if (!digits) return '';
    return digits.match(/.{1,4}/g)?.join(' ') || digits;
  }

  getTransactionIcon(type: number): string {
    switch (this.getNormalizedTransactionType(type)) {
      case 0: return 'shopping_cart';
      case 1: return 'account_balance_wallet';
      case 2: return 'keyboard_return';
      default: return 'payments';
    }
  }

  getNormalizedTransactionType(type: number | string): 0 | 1 | 2 {
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

    if (lowered.startsWith('saga:') && this.getNormalizedTransactionType(tx.type) === 1) {
      return `Bill Statement of ${this.getMonthYear(tx.dateUtc)}`;
    }

    return description || 'Card Transaction';
  }

  getTransactionFlowLabel(type: number | string): string {
    return this.getNormalizedTransactionType(type) === 0 ? 'Debit' : 'Credit';
  }

  getTransactionTypeLabel(type: number | string): string {
    const normalized = this.getNormalizedTransactionType(type);
    if (normalized === 0) return 'Purchase';
    if (normalized === 1) return 'Payment';
    return 'Refund';
  }

  getSignedAmountPrefix(type: number | string): string {
    return this.getNormalizedTransactionType(type) === 0 ? '-' : '+';
  }

  private getMonthYear(dateUtc: string): string {
    return formatIstDate(dateUtc, 'MMM yyyy', '-');
  }

  page() { return this.currentPage(); }
  
  paginatedTransactions() {
    const start = (this.currentPage() - 1) * this.itemsPerPage;
    return this.currentCardTransactions().slice(start, start + this.itemsPerPage);
  }

  totalPages(): number {
    return Math.ceil(this.currentCardTransactions().length / this.itemsPerPage);
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

  getProgressBarColor(category: string): string {
    switch (category) {
      case 'Dining': return 'bg-charcoal';
      case 'Shopping': return 'bg-primary';
      case 'Travel': return 'bg-emerald-500';
      case 'Housing': return 'bg-indigo-500';
      case 'Utilities': return 'bg-amber-500';
      default: return 'bg-misty-gray';
    }
  }

  // ==================== PAYMENT METHODS ====================

  getBillOutstandingAmount(bill: Bill): number {
    return Math.max(0, Number(bill.amount) - this.getBillAmountPaid(bill));
  }

  getPaymentAmount(bill: Bill | null, type: 'full' | 'min'): number {
    if (!bill) return 0;

    const outstanding = this.getBillOutstandingAmount(bill);
    if (type === 'full') {
      return outstanding;
    }

    return Math.min(outstanding, Number(bill.minDue));
  }

  getRewardsDeductionAmount(bill: Bill | null, type: 'full' | 'min'): number {
    if (!bill || !this.useRewards()) return 0;
    const paymentAmount = this.getPaymentAmount(bill, type);
    const deduction = Math.min(this.maxRedeemablePoints() * this.pointsToRupeeRate, paymentAmount);
    return Number(deduction.toFixed(2));
  }

  getNetPayableAmount(bill: Bill | null, type: 'full' | 'min'): number {
    const paymentAmount = this.getPaymentAmount(bill, type);
    const netPayable = paymentAmount - this.getRewardsDeductionAmount(bill, type);
    return Number(Math.max(0, netPayable).toFixed(2));
  }

  canSelectMinimumDue(bill: Bill | null): boolean {
    if (!bill) return false;
    if (this.getBillOutstandingAmount(bill) <= 0) return false;
    return this.getBillAmountPaid(bill) <= 0;
  }

  getSelectedBillOutstanding(): number {
    return this.getPaymentAmount(this.currentBill(), 'full');
  }

  private getBillAmountPaid(bill: Bill): number {
    return Number(bill.amountPaid || 0);
  }

  toggleRewards(): void {
    this.useRewards.update(value => !value);
  }

  resendOtp(): void {
    if (!this.canResendOtp()) return;
    this.canResendOtp.set(false);
    setTimeout(() => this.canResendOtp.set(true), 30000);
  }
  
  openPaymentModal(): void {
    this.showPaymentModal.set(true);
    this.paymentError.set(null);
    this.paymentType.set('full');
    this.paymentStage.set('initiated');
    this.useRewards.set(false);
    this.otpCode = '';
    this.otpError.set(null);
    this.loadRewards();
  }

  closePaymentModal(): void {
    this.showPaymentModal.set(false);
    this.resetPaymentState();
  }

  initiatePayment(): void {
    const card = this.card();
    const bill = this.currentBill();
    
    if (!card || !bill) return;

    const amount = this.getPaymentAmount(bill, this.paymentType());

    if (this.paymentType() === 'min' && !this.canSelectMinimumDue(bill)) {
      this.paymentError.set('Minimum due can only be paid once. Please pay the full remaining amount.');
      this.paymentType.set('full');
      return;
    }

    if (amount <= 0) {
      this.paymentError.set('This bill is already settled.');
      return;
    }

    this.isProcessingPayment.set(true);
    this.paymentError.set(null);
    this.paymentStage.set('processing');

    const rewardsPoints = this.useRewards() ? this.maxRedeemablePoints() : null;

    const request: PaymentInitiateRequest = {
      cardId: card.id,
      billId: bill.id,
      amount,
      paymentType: this.paymentType() === 'full' ? 'Full' : 'Partial',
      rewardsPoints
    };

    this.paymentService.initiatePayment(request).pipe(
      finalize(() => this.isProcessingPayment.set(false))
    ).subscribe({
      next: (res: ApiResponse<PaymentInitiateResponse>) => {
        if (res.success && res.data) {
          this.currentPaymentId.set(res.data.paymentId);
          this.paymentStage.set('otp_sent');
          this.showPaymentModal.set(false);
          this.showOtpModal.set(true);
        } else {
          this.paymentError.set(res.message || 'Failed to initiate payment');
          this.paymentStage.set('initiated');
          this.scrollToError();
        }
      },
      error: (err: any) => {
        const backendMessage = err?.error?.message
          || err?.error?.data?.message
          || err?.message;
        this.paymentError.set(backendMessage || 'Network error. Please try again.');
        this.paymentStage.set('initiated');
        this.scrollToError();
      }
    });
  }

  private scrollToError(): void {
    setTimeout(() => {
      const errorBox = document.getElementById('payment-error-box');
      if (errorBox) {
        errorBox.scrollIntoView({ behavior: 'smooth', block: 'end' });
      }
    }, 100);
  }

  verifyOtp(): void {
    if (this.otpCode.length !== 6) {
      this.otpError.set('Please enter a valid 6-digit OTP');
      return;
    }

    const paymentId = this.currentPaymentId();
    if (!paymentId) return;

    this.isVerifyingOtp.set(true);
    this.otpError.set(null);

    this.paymentService.verifyOtp(paymentId, this.otpCode).subscribe({
      next: (res: ApiResponse<any>) => {
        this.isVerifyingOtp.set(false);
        if (res.success) {
          this.showOtpModal.set(false);
          this.showSuccessModal.set(true);

          const bill = this.currentBill();
          this.paidAmount.set(this.getNetPayableAmount(bill, this.paymentType()));

          if (paymentId) {
            this.startPaymentStatusPolling(paymentId);
          } else {
            this.refreshCardAndBillData();
          }
        } else {
          this.otpError.set(res.message || 'Invalid OTP');
        }
      },
      error: () => {
        this.isVerifyingOtp.set(false);
        this.otpError.set('Verification failed. Please try again.');
      }
    });
  }

  closeSuccessModal(): void {
    this.showSuccessModal.set(false);
    this.refreshCardAndBillData();
    this.resetPaymentState();
  }

  private refreshCardAndBillData(): void {
    const cardId = this.card()?.id;
    if (!cardId) return;

    this.loadCardDetails(cardId);
    this.loadBillForCard(cardId);
    this.loadRewards();
  }

  private startPaymentStatusPolling(paymentId: string): void {
    this.stopPaymentStatusPolling();
    this.paymentStatusPollAttempts = 0;

    const pollStatus = () => {
      this.paymentService.getPaymentById(paymentId).subscribe({
        next: (res: ApiResponse<any>) => {
          const status = String(res.data?.status || '').toLowerCase();

          if (status === 'completed') {
            this.paymentStage.set('completed');
            this.refreshCardAndBillData();
            this.stopPaymentStatusPolling();
            return;
          }

          if (status === 'failed' || status === 'reversed' || status === 'cancelled') {
            this.stopPaymentStatusPolling();
            this.showSuccessModal.set(false);
            this.paymentError.set('Payment could not be completed. Please try again.');
            this.refreshCardAndBillData();
            return;
          }

          this.paymentStatusPollAttempts += 1;
          if (this.paymentStatusPollAttempts >= this.maxPaymentStatusPollAttempts) {
            this.refreshCardAndBillData();
            this.stopPaymentStatusPolling();
          }
        },
        error: () => {
          this.paymentStatusPollAttempts += 1;
          if (this.paymentStatusPollAttempts >= this.maxPaymentStatusPollAttempts) {
            this.refreshCardAndBillData();
            this.stopPaymentStatusPolling();
          }
        }
      });
    };

    pollStatus();
    this.paymentStatusPollTimer = setInterval(pollStatus, this.paymentStatusPollIntervalMs);
  }

  private stopPaymentStatusPolling(): void {
    if (!this.paymentStatusPollTimer) return;

    clearInterval(this.paymentStatusPollTimer);
    this.paymentStatusPollTimer = null;
  }

  private resetPaymentState(): void {
    this.paymentError.set(null);
    this.otpError.set(null);
    this.otpCode = '';
    this.currentPaymentId.set(null);
    this.paymentStage.set('initiated');
    this.isProcessingPayment.set(false);
    this.isVerifyingOtp.set(false);
    this.canResendOtp.set(true);
    this.useRewards.set(false);
    this.paymentType.set('full');
  }

  getEffectiveBillStatus(bill: Bill): number {
    const outstanding = Math.max(0, Number(bill.amount) - Number(bill.amountPaid || 0));
    if (outstanding <= 0) return BillStatus.Paid;

    const paidAmount = Number(bill.amountPaid || 0);
    const dueDate = new Date(bill.dueDateUtc);
    const now = new Date();

    if (dueDate.getTime() < now.getTime()) return BillStatus.Overdue;
    if (paidAmount > 0) return BillStatus.PartiallyPaid;

    return BillStatus.Pending;
  }

  getBillStatusLabel(status: number): string {
    switch (status) {
      case BillStatus.Pending: return 'Pending';
      case BillStatus.Paid: return 'Paid';
      case BillStatus.Overdue: return 'Overdue';
      case BillStatus.PartiallyPaid: return 'Partial';
      case BillStatus.Cancelled: return 'Cancelled';
      default: return 'Unknown';
    }
  }

  getBillStatusClass(status: number): string {
    switch (status) {
      case BillStatus.Pending: return 'status-pending';
      case BillStatus.Paid: return 'status-paid';
      case BillStatus.Overdue: return 'status-overdue';
      case BillStatus.PartiallyPaid: return 'status-partial';
      default: return 'status-unknown';
    }
  }

  hasActiveBill = computed(() => {
    const bill = this.currentBill();
    return bill !== null && bill !== undefined;
  });

  /** Detect if the error is a wallet balance issue */
  isWalletInsufficientError(message: string): boolean {
    return message.toLowerCase().includes('insufficient wallet balance') || 
           message.toLowerCase().includes('wallet balance');
  }

  /** Extract a user-friendly message from the wallet error */
  getWalletErrorMessage(message: string): string {
    // Parse "Available: 0.00, required: 11000.00" style messages
    const availableMatch = message.match(/available:\s*([0-9,.]+)/i);
    const requiredMatch = message.match(/required:\s*([0-9,.]+)/i);
    if (availableMatch && requiredMatch) {
      const available = parseFloat(availableMatch[1].replace(',', ''));
      const required = parseFloat(requiredMatch[1].replace(',', ''));
      const deficit = (required - available).toFixed(2);
      return `Your wallet balance is ₹${available.toLocaleString('en-IN', {minimumFractionDigits: 2})}. You need ₹${deficit} more to complete this payment. Please top up your wallet first.`;
    }
    return 'Your wallet balance is insufficient for this payment. Please top up your wallet and try again.';
  }
}
