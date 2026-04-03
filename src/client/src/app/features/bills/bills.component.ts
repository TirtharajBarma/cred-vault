import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { BillingService, Bill, BillStatus } from '../../core/services/billing.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { PaymentService } from '../../core/services/payment.service';
import { CreditCard } from '../../core/models/card.models';

type MilestoneDisplay = {
  date: Date;
  cardLabel: string;
  mode: 'due_date_queue' | 'next_bill_generation' | 'latest_statement_date';
};

@Component({
  selector: 'app-bills',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './bills.component.html',
  styleUrls: ['./bills.component.css']
})
export class BillsComponent implements OnInit {
  private billingService = inject(BillingService);
  private dashboardService = inject(DashboardService);
  private paymentService = inject(PaymentService);
  private router = inject(Router);

  bills = signal<Bill[]>([]);
  cards = signal<CreditCard[]>([]);
  isLoading = signal(true);
  filterStatus = signal<number | 'due' | null>(null);
  currentPage = signal(1);
  itemsPerPage = 7;

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
      .sort((a, b) => new Date(a.dueDateUtc).getTime() - new Date(b.dueDateUtc).getTime());

    // Priority 1: Always show nearest unpaid due date first.
    if (unpaidBills.length > 0) {
      const nextDueBill = unpaidBills[0];

      return {
        date: new Date(nextDueBill.dueDateUtc),
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
      .sort((a, b) => new Date(b.billingDateUtc).getTime() - new Date(a.billingDateUtc).getTime())[0];

    if (latestStatement) {
      return {
        date: new Date(latestStatement.billingDateUtc),
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

      const aDue = new Date(a.dueDateUtc).getTime();
      const bDue = new Date(b.dueDateUtc).getTime();

      if (aPriority <= 2) {
        // For active dues, nearest date first.
        return aDue - bDue;
      }

      // For paid/cancelled rows, show latest first.
      return bDue - aDue;
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

  getCountdownLabel(date: string): string {
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
      return this.getCountdownLabel(milestone.date.toISOString());
    }

    if (milestone.mode === 'latest_statement_date') {
      return 'latest generated statement date';
    }

    return this.getCountdownLabel(milestone.date.toISOString());
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

  private getDayDiff(dateIso: string): number {
    const target = new Date(dateIso);
    const today = new Date();
    const targetDate = new Date(target.getFullYear(), target.getMonth(), target.getDate());
    const todayDate = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const diffMs = targetDate.getTime() - todayDate.getTime();
    return Math.round(diffMs / (1000 * 60 * 60 * 24));
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
    const dueDate = new Date(bill.dueDateUtc);
    const now = new Date();

    if (dueDate.getTime() < now.getTime()) return BillStatus.Overdue;
    if (paidAmount > 0) return BillStatus.PartiallyPaid;

    return BillStatus.Pending;
  }

  private normalizeBillStatus(status: number | string): BillStatus {
    if (status === BillStatus.Pending || status === 1 || status === '1' || status === 'Pending') return BillStatus.Pending;
    if (status === BillStatus.Paid || status === 2 || status === '2' || status === 'Paid') return BillStatus.Paid;
    if (status === BillStatus.Overdue || status === 3 || status === '3' || status === 'Overdue') return BillStatus.Overdue;
    if (status === BillStatus.PartiallyPaid || status === 5 || status === '5' || status === 'PartiallyPaid') return BillStatus.PartiallyPaid;
    return BillStatus.Cancelled;
  }

  viewStatement(bill: Bill): void {
    this.router.navigate(['/statements', bill.id]);
  }

  openPayment(bill: Bill): void {
    this.selectedBill.set(bill);
    this.paymentType.set('full');
    this.errorMessage.set(null);
    this.paymentStage.set('initiated');
    this.showPaymentModal.set(true);
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

    if (amount <= 0) {
      this.errorMessage.set('This bill is already settled.');
      return;
    }
    
    this.isProcessing.set(true);
    this.errorMessage.set(null);
    this.paymentStage.set('processing');

    this.paymentService.initiatePayment({
      cardId: card.id,
      billId: bill.id,
      amount: amount,
      paymentType: this.paymentType() === 'full' ? 'Full' : 'Partial'
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
        }
      },
      error: (err) => {
        this.isProcessing.set(false);
        this.errorMessage.set(err?.error?.message || 'Server error');
        this.paymentStage.set('initiated');
      }
    });
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
          this.paidAmount.set(this.getPaymentAmount(this.selectedBill(), this.paymentType()));
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
          },
          error: () => this.isLoading.set(false)
        });
      },
      error: () => this.isLoading.set(false)
    });
  }

  setFilter(status: number | 'due' | null): void {
    this.filterStatus.set(status);
    this.currentPage.set(1);
  }
}