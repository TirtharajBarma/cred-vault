import { Component, OnInit, OnDestroy, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { BillingService, Bill, BillStatus } from '../../core/services/billing.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { PaymentService } from '../../core/services/payment.service';
import { RewardsService, StatementService, Statement } from '../../core/services/rewards.service';
import { WalletService } from '../../core/services/wallet.service';
import { CreditCard } from '../../core/models/card.models';
import { IstDatePipe } from '../../shared/pipes/ist-date.pipe';
import { DateInput, getIstEpochDay, getUtcTimestamp, parseUtcDate } from '../../core/utils/date-time.util';

type MilestoneDisplay = {
  date: Date;
  cardLabel: string;
  mode: 'due_date_queue' | 'next_bill_generation' | 'latest_statement_date';
};

import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-bills',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, IstDatePipe],
  templateUrl: './bills.component.html',
  styleUrls: ['./bills.component.css']
})
export class BillsComponent implements OnInit, OnDestroy {
  private billingService = inject(BillingService);
  private dashboardService = inject(DashboardService);
  private paymentService = inject(PaymentService);
  private rewardsService = inject(RewardsService);
  private statementService = inject(StatementService);
  private walletService = inject(WalletService);
  private router = inject(Router);

  bills = signal<Bill[]>([]);
  cards = signal<CreditCard[]>([]);
  walletBalance = signal(0);
  hasWallet = signal(false);
  isLoading = signal(true);
  filterStatus = signal<number | 'due' | null>(null);
  currentPage = signal(1);
  itemsPerPage = 7;
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  showPaymentModal = signal(false);
  selectedBill = signal<Bill | null>(null);
  paymentType = signal<'full' | 'min'>('full');
  isProcessing = signal(false);
  errorMessage = signal<string | null>(null);

  showOtpModal = signal(false);
  showSuccessModal = signal(false);
  paymentId = signal<string | null>(null);
  otpCode = '';
  isVerifying = signal(false);
  otpError = signal<string | null>(null);
  paidAmount = signal(0);

  paymentStage = signal<'initiated' | 'otp_sent' | 'processing' | 'completed'>('initiated');
  canResendOtp = signal(true);

  useRewards = signal(false);
  availablePoints = signal(0);
  readonly pointsToRupeeRate = environment.pointsToRupeeRate;
  
  availableRedeemablePoints = computed(() => Math.floor(this.availablePoints()));
  
  rewardValue = computed(() => this.availableRedeemablePoints() * this.pointsToRupeeRate);
  
  usablePointsValue = computed(() => {
    const bill = this.selectedBill();
    if (!bill) return 0;
    const outstanding = this.getBillOutstandingAmount(bill);
    return Math.min(this.rewardValue(), outstanding);
  });
  
  maxRedeemablePoints = computed(() => {
    const bill = this.selectedBill();
    if (!bill) return 0;
    const selectedPaymentAmount = this.getPaymentAmount(bill, this.paymentType());
    const maxPointsByAmount = Math.floor(selectedPaymentAmount / this.pointsToRupeeRate);
    return Math.min(this.availableRedeemablePoints(), maxPointsByAmount);
  });

  resendOtp(): void {
    if (!this.canResendOtp()) return;
    this.canResendOtp.set(false);
    setTimeout(() => this.canResendOtp.set(true), 30000);
  }

  totalOutstanding = computed(() => {
    return this.bills()
      .filter(b => this.isBillPayable(b))
      .reduce((acc, b) => acc + this.getBillOutstandingAmount(b), 0);
  });

  milestoneDisplay = computed<MilestoneDisplay | null>(() => {
    const allBills = this.bills();
    const unpaidBills = allBills
      .filter(b => [BillStatus.Pending, BillStatus.Overdue, BillStatus.PartiallyPaid].includes(b.status))
      .filter(b => !!b.dueDateUtc)
      .sort((a, b) => getUtcTimestamp(a.dueDateUtc) - getUtcTimestamp(b.dueDateUtc));

    // Priority 1: Always show nearest unpaid due date first.
    if (unpaidBills.length > 0) {
      const nextDueBill = unpaidBills[0];

      return {
        date: parseUtcDate(nextDueBill.dueDateUtc),
        cardLabel: this.getPayeeInfo(nextDueBill).name,
        mode: 'due_date_queue'
      };
    }

    // Priority 2: If all bills are paid, forecast nearest next bill generation date from cards.
    const cards = this.cards();
    if (cards.length > 0) {
      const candidates = cards.map(card => {
        const day = this.getBillingCycleDay(card);
        const nextDate = this.getNextBillingGenerationDate(day);
        return {
          date: nextDate,
          cardLabel: `${card.issuerName || 'Card'} *${card.last4}`
        };
      });

      candidates.sort((a, b) => a.date.getTime() - b.date.getTime());

      return {
        date: candidates[0].date,
        cardLabel: candidates[0].cardLabel,
        mode: 'next_bill_generation'
      };
    }

    // Safety fallback: if cards unavailable but bills exist, show latest statement generation date.
    const latestStatement = allBills
      .filter(b => !!b.billingDateUtc)
      .sort((a, b) => getUtcTimestamp(b.billingDateUtc) - getUtcTimestamp(a.billingDateUtc))[0];

    if (latestStatement) {
      return {
        date: parseUtcDate(latestStatement.billingDateUtc),
        cardLabel: this.getPayeeInfo(latestStatement).name,
        mode: 'latest_statement_date'
      };
    }

    return null;
  });

  filteredBills() {
    const status = this.filterStatus();
    const source = status === null
      ? this.bills()
      : status === 'due'
        ? this.bills().filter(b => this.isBillPayable(b))
        : this.bills().filter(b => this.getEffectiveBillStatus(b) === status);

    return [...source].sort((a, b) => {
      const aPriority = this.getBillPriority(a);
      const bPriority = this.getBillPriority(b);
      
      if (aPriority !== bPriority) return aPriority - bPriority;

      // For paid bills, always show the most recently settled/updated records first.
      if (aPriority === 2) {
        const aPaidTime = this.getBillLatestSortTime(a);
        const bPaidTime = this.getBillLatestSortTime(b);
        if (aPaidTime !== bPaidTime) return bPaidTime - aPaidTime;
      }

      const aDate = getUtcTimestamp(a.billingDateUtc);
      const bDate = getUtcTimestamp(b.billingDateUtc);

      if (aDate !== bDate) return bDate - aDate;

      // Deterministic fallback when billing cycle date is identical.
      return this.getBillLatestSortTime(b) - this.getBillLatestSortTime(a);
    });
  }

  paginatedBills() {
    const start = (this.currentPage() - 1) * this.itemsPerPage;
    return this.filteredBills().slice(start, start + this.itemsPerPage);
  }

  totalPages(): number {
    return Math.ceil(this.filteredBills().length / this.itemsPerPage);
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

  getCardInfo(cardId: string): { name: string; last4: string; network: string } | null {
    const card = this.cards().find(c => c.id === cardId);
    if (card) {
      return {
        name: card.issuerName,
        last4: card.last4,
        network: card.network
      };
    }
    return null;
  }

  getPayeeInfo(bill: Bill): { name: string; sub: string; icon: string; category: string } {
    const cardInfo = this.getCardInfo(bill.cardId);
    if (cardInfo) {
      return {
        name: `${cardInfo.name} *${cardInfo.last4}`,
        sub: cardInfo.network,
        icon: 'credit_card',
        category: cardInfo.network.toUpperCase()
      };
    }
    return {
      name: 'Credit Card Payment',
      sub: 'Monthly Bill',
      icon: 'credit_card',
      category: 'CREDIT'
    };
  }

  getDaysRemaining(date: string): string {
    const days = this.getDayDiff(date);
    if (days < 0) return `OVERDUE BY ${Math.abs(days)} DAY${Math.abs(days) === 1 ? '' : 'S'}`;
    if (days === 0) return 'DUE TODAY';
    if (days === 1) return 'DUE IN 1 DAY';
    return `DUE IN ${days} DAYS`;
  }

  getCountdownLabel(date: DateInput): string {
    const days = this.getDayDiff(date);
    if (days < 0) return `overdue by ${Math.abs(days)} day${Math.abs(days) === 1 ? '' : 's'}`;
    if (days === 0) return 'today';
    if (days === 1) return 'in 1 day';
    return `in ${days} days`;
  }

  getUpcomingMilestoneSubtitle(): string {
    const milestone = this.milestoneDisplay();
    if (!milestone) return 'No billing milestones yet';

    if (milestone.mode === 'due_date_queue') {
      return this.getCountdownLabel(milestone.date);
    }

    if (milestone.mode === 'latest_statement_date') {
      return 'latest generated statement date';
    }

    return this.getCountdownLabel(milestone.date);
  }

  getUpcomingMilestoneSupportText(): string {
    const milestone = this.milestoneDisplay();
    if (!milestone) return '';

    if (milestone.mode === 'due_date_queue') {
      return 'Priority queue: unpaid due dates';
    }

    if (milestone.mode === 'next_bill_generation') {
      return 'All dues are clear. Tracking next bill generation.';
    }

    return 'Showing latest statement timeline.';
  }

  getUpcomingMilestoneCardLabel(): string {
    const milestone = this.milestoneDisplay();
    if (!milestone) return 'No active card milestone';
    return milestone.cardLabel;
  }

  getMilestoneBadgeLabel(): string {
    const milestone = this.milestoneDisplay();
    if (!milestone) return 'No Milestone';
    if (milestone.mode === 'due_date_queue') return 'Upcoming Due Date';
    if (milestone.mode === 'next_bill_generation') return 'Next Bill Generation';
    return 'Latest Statement Date';
  }

  getMilestoneDate(): Date | null {
    return this.milestoneDisplay()?.date ?? null;
  }

  isBillPayable(bill: Bill): boolean {
    return this.getBillOutstandingAmount(bill) > 0;
  }

  isBillPaid(bill: Bill): boolean {
    return this.getEffectiveBillStatus(bill) === BillStatus.Paid;
  }

  getBillOutstandingAmount(bill: Bill): number {
    return Math.max(0, Number(bill.amount) - this.getBillAmountPaid(bill));
  }

  getBillSecondaryStatusText(bill: Bill): string {
    return this.getEffectiveBillStatus(bill) === BillStatus.Paid ? 'PAID' : this.getDaysRemaining(bill.dueDateUtc);
  }

  private getDayDiff(dateInput: DateInput): number {
    const target = getIstEpochDay(dateInput);
    const today = getIstEpochDay(new Date());

    if (Number.isNaN(target) || Number.isNaN(today)) return 0;
    return target - today;
  }

  private getBillingCycleDay(card: CreditCard): number {
    const day = Number(card.billingCycleStartDay ?? 1);
    if (!Number.isFinite(day)) return 1;
    return Math.min(31, Math.max(1, day));
  }

  private getNextBillingGenerationDate(day: number): Date {
    const now = new Date();
    const year = now.getFullYear();
    const month = now.getMonth();

    const safeDayThisMonth = Math.min(day, new Date(year, month + 1, 0).getDate());
    const candidateThisMonth = new Date(year, month, safeDayThisMonth, 0, 0, 0, 0);

    if (candidateThisMonth.getTime() >= now.getTime()) {
      return candidateThisMonth;
    }

    const safeDayNextMonth = Math.min(day, new Date(year, month + 2, 0).getDate());
    return new Date(year, month + 1, safeDayNextMonth, 0, 0, 0, 0);
  }

  private getBillPriority(bill: Bill): number {
    const status = this.getEffectiveBillStatus(bill);
    if (status === BillStatus.Overdue) return 0;
    if (status === BillStatus.Pending || status === BillStatus.PartiallyPaid) return 1;
    if (status === BillStatus.Paid) return 2;
    return 3;
  }

  private getBillLatestSortTime(bill: Bill): number {
    const candidates = [bill.paidAtUtc, bill.updatedAtUtc, bill.createdAtUtc, bill.billingDateUtc];
    for (const value of candidates) {
      if (!value) continue;
      const timestamp = getUtcTimestamp(value);
      if (timestamp > 0) return timestamp;
    }
    return 0;
  }

  getBillStatusLabel(bill: Bill): string {
    switch (this.getEffectiveBillStatus(bill)) {
      case BillStatus.Pending: return 'Pending';
      case BillStatus.Paid: return 'Paid';
      case BillStatus.Overdue: return 'Overdue';
      case BillStatus.PartiallyPaid: return 'Partial';
      default: return 'Unknown';
    }
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
    return this.getPaymentAmount(this.selectedBill(), 'full');
  }

  private getBillAmountPaid(bill: Bill): number {
    return Number(bill.amountPaid || 0);
  }

  private getEffectiveBillStatus(bill: Bill): BillStatus {
    const outstanding = this.getBillOutstandingAmount(bill);
    if (outstanding <= 0) return BillStatus.Paid;

    const paidAmount = this.getBillAmountPaid(bill);
    const dueDate = parseUtcDate(bill.dueDateUtc);
    const now = new Date();

    if (dueDate.getTime() < now.getTime()) return BillStatus.Overdue;
    if (paidAmount > 0) return BillStatus.PartiallyPaid;

    return BillStatus.Pending;
  }

  private normalizeBillStatus(status: number | string): BillStatus {
    if (status === BillStatus.Pending || status === 0 || status === '0' || status === 'Pending') return BillStatus.Pending;
    if (status === BillStatus.Paid || status === 1 || status === '1' || status === 'Paid') return BillStatus.Paid;
    if (status === BillStatus.Overdue || status === 2 || status === '2' || status === 'Overdue') return BillStatus.Overdue;
    if (status === BillStatus.PartiallyPaid || status === 4 || status === '4' || status === 'PartiallyPaid') return BillStatus.PartiallyPaid;

    // Backward compatibility for old frontend-mapped numeric statuses.
    if (status === 3 || status === '3' || status === 'Cancelled') return BillStatus.Cancelled;
    if (status === 5 || status === '5') return BillStatus.PartiallyPaid;

    return BillStatus.Cancelled;
  }

  viewStatement(bill: Bill): void {
    this.statementService.getStatementByBillId(bill.id).subscribe({
      next: (res) => {
        if (!res.success || !res.data) {
          this.router.navigate(['/statements']);
          return;
        }

        const data = Array.isArray(res.data) ? res.data : [res.data];
        const statement = data[0] as Statement | undefined;

        if (statement?.id) {
          this.router.navigate(['/statements', statement.id], {
            queryParams: { tab: 'transactions' }
          });
          return;
        }

        this.router.navigate(['/statements']);
      },
      error: () => this.router.navigate(['/statements'])
    });
  }

  openPayment(bill: Bill): void {
    this.selectedBill.set(bill);
    this.paymentType.set('full');
    this.errorMessage.set(null);
    this.paymentStage.set('initiated');
    this.useRewards.set(false);
    this.loadRewardAccount();
    this.showPaymentModal.set(true);
  }

  private loadRewardAccount(): void {
    this.rewardsService.getRewardAccount().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.availablePoints.set(res.data.pointsBalance || 0);
        }
      },
      error: () => this.availablePoints.set(0)
    });
  }

  toggleRewards(): void {
    this.useRewards.update(v => !v);
  }

  closePayment(): void {
    this.showPaymentModal.set(false);
    this.selectedBill.set(null);
  }

  initiatePayment(): void {
    if (!this.selectedBill()) return;
    
    const bill = this.selectedBill()!;
    const card = this.cards().find(c => c.id === bill.cardId);
    
    if (!card) {
      this.errorMessage.set('Card associated with this bill not found');
      return;
    }
    
    const amount = this.getPaymentAmount(bill, this.paymentType());

    if (this.paymentType() === 'min' && !this.canSelectMinimumDue(bill)) {
      this.errorMessage.set('Minimum due can only be paid once. Please pay the full remaining amount.');
      this.paymentType.set('full');
      return;
    }

    if (amount <= 0) {
      this.errorMessage.set('This bill is already settled.');
      return;
    }
    
    this.isProcessing.set(true);
    this.errorMessage.set(null);
    this.paymentStage.set('processing');

    const rewardsPoints = this.useRewards() ? this.maxRedeemablePoints() : null;

    this.paymentService.initiatePayment({
      cardId: card.id,
      billId: bill.id,
      amount: amount,
      paymentType: this.paymentType() === 'full' ? 'Full' : 'Partial',
      rewardsPoints: rewardsPoints
    }).subscribe({
      next: (res) => {
        this.isProcessing.set(false);
        if (res.success && res.data) {
          this.paymentId.set(res.data.paymentId);
          this.paymentStage.set('otp_sent');
          this.showPaymentModal.set(false);
          this.showOtpModal.set(true);
        } else {
          this.errorMessage.set(res.message || 'Payment initiation failed');
          this.paymentStage.set('initiated');
          this.scrollToError();
        }
      },
      error: (err) => {
        this.isProcessing.set(false);
        const backendMessage = err?.error?.message || err?.error?.data?.message || err?.message;
        this.errorMessage.set(backendMessage || 'Failed to initiate payment. Please try again.');
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
    }, 100); // 100ms gives Angular enough time to render the *ngIf / @if block
  }

  verifyOtp(): void {
    if (this.otpCode.length !== 6 || !this.paymentId()) return;

    this.isVerifying.set(true);
    this.otpError.set(null);

    this.paymentService.verifyOtp(this.paymentId()!, this.otpCode).subscribe({
      next: (res) => {
        this.isVerifying.set(false);
        if (res.success) {
          this.showOtpModal.set(false);
          this.showSuccessModal.set(true);
          this.paidAmount.set(this.getNetPayableAmount(this.selectedBill(), this.paymentType()));
          this.loadData();
        } else {
          this.otpError.set(res.message || 'Invalid OTP');
        }
      },
      error: (err) => {
        this.isVerifying.set(false);
        this.otpError.set(err?.error?.message || 'Verification failed');
      }
    });
  }

  closeSuccessModal(): void {
    this.showSuccessModal.set(false);
    this.resetState();
    this.loadData();
  }

  resetState(): void {
    this.selectedBill.set(null);
    this.paymentType.set('full');
    this.otpCode = '';
    this.paymentId.set(null);
  }

  ngOnInit(): void {
    this.loadData();
    this.loadWalletBalance();
    this.refreshTimer = setInterval(() => this.loadData(), 30000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) { clearInterval(this.refreshTimer); this.refreshTimer = null; }
  }

  loadData(): void {
    this.isLoading.set(true);
    this.dashboardService.getCards().subscribe({
      next: (resCards) => {
        this.cards.set(resCards.data || []);
        this.billingService.getMyBills().subscribe({
          next: (res) => {
            this.bills.set(res.data || []);
            this.isLoading.set(false);
            this.loadWalletBalance();
          },
          error: () => this.isLoading.set(false)
        });
      },
      error: () => this.isLoading.set(false)
    });
  }

  loadWalletBalance(): void {
    this.walletService.getBalance().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.walletBalance.set(Number(res.data.balance || 0));
          this.hasWallet.set(Boolean(res.data.hasWallet));
        }
      },
      error: () => {
        this.walletBalance.set(0);
        this.hasWallet.set(false);
      }
    });
  }

  openWalletActivity(): void {
    this.router.navigate(['/bills/wallet']);
  }

  setFilter(status: number | 'due' | null): void {
    this.filterStatus.set(status);
    this.currentPage.set(1);
  }

  /** Detect if the error is a wallet balance issue */
  isWalletInsufficientError(message: string): boolean {
    return message.toLowerCase().includes('insufficient wallet balance') ||
           message.toLowerCase().includes('wallet balance');
  }

  /** Extract a user-friendly message from the wallet error */
  getWalletErrorMessage(message: string): string {
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