import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { PaymentService, Payment } from '../../core/services/payment.service';
import { BillingService, Bill } from '../../core/services/billing.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { CreditCard } from '../../core/models/card.models';

@Component({
  selector: 'app-payments',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './payments.component.html'
})
export class PaymentsComponent implements OnInit {
  private paymentService = inject(PaymentService);
  private billingService = inject(BillingService);
  private dashboardService = inject(DashboardService);

  activeTab = signal<'bills' | 'history'>('bills');
  bills = signal<Bill[]>([]);
  payments = signal<Payment[]>([]);
  cards = signal<CreditCard[]>([]);
  isLoading = signal(true);

  showPaymentModal = signal(false);
  selectedBill = signal<Bill | null>(null);
  paymentStep = signal(1);
  selectedAmount = signal(0);
  paymentType = signal<'full' | 'min'>('full');
  selectedCard = signal<CreditCard | null>(null);
  
  isProcessing = signal(false);
  errorMessage = signal<string | null>(null);

  showOtpModal = signal(false);
  showSuccessModal = signal(false);
  showBillDetailModal = signal(false);
  showPaymentDetailModal = signal(false);
  selectedBillDetail = signal<Bill | null>(null);
  selectedPaymentDetail = signal<Payment | null>(null);
  devOtp = signal<string | null>(null);
  paymentId = signal<string | null>(null);
  
  otpCode = '';
  isVerifying = signal(false);
  otpError = signal<string | null>(null);
  paidAmount = signal(0);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    
    forkJoin({
      bills: this.billingService.getMyBills().pipe(catchError(() => of({ success: false, data: [] }))),
      payments: this.paymentService.getMyPayments().pipe(catchError(() => of({ success: false, data: [] }))),
      cards: this.dashboardService.getCards().pipe(catchError(() => of({ success: false, data: [] })))
    }).subscribe(res => {
      if (res.bills.success) this.bills.set(res.bills.data || []);
      if (res.payments.success) this.payments.set(res.payments.data || []);
      if (res.cards.success) this.cards.set(res.cards.data || []);
      this.isLoading.set(false);
    });
  }

  pendingBills(): Bill[] {
    return this.bills().filter(b => b.status === 1);
  }

  availableCards(): CreditCard[] {
    return this.cards().filter(c => c.creditLimit > c.outstandingBalance);
  }

  getAvailableCredit(card: CreditCard): number {
    return card.creditLimit - card.outstandingBalance;
  }

  getCardName(bill: Bill | null): string {
    if (!bill) return '';
    const card = this.cards().find(c => c.id === bill.cardId);
    if (!card) return `Card ending in ${this.getCardLast4(bill.cardId)}`;
    return `${card.issuerName} ${card.network} *${card.last4}`;
  }

  getCardLast4(cardId: string): string {
    const card = this.cards().find(c => c.id === cardId);
    return card?.last4 || '????';
  }

  isOverdue(bill: Bill): boolean {
    return new Date(bill.dueDateUtc) < new Date();
  }

  getStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'completed': return 'bg-green-100 text-green-700';
      case 'failed':
      case 'reversed': return 'bg-red-100 text-red-700';
      default: return 'bg-amber-100 text-amber-700';
    }
  }

  getStatusIconBg(status: string): string {
    switch (status.toLowerCase()) {
      case 'completed': return 'bg-green-100';
      default: return 'bg-red-100';
    }
  }

  openPayment(bill: Bill): void {
    this.selectedBill.set(bill);
    this.selectedAmount.set(bill.amount);
    this.paymentType.set('full');
    this.selectedCard.set(null);
    this.paymentStep.set(1);
    this.errorMessage.set(null);
    this.showPaymentModal.set(true);
  }

  closePayment(): void {
    this.showPaymentModal.set(false);
    this.selectedBill.set(null);
    this.selectedAmount.set(0);
    this.selectedCard.set(null);
    this.paymentStep.set(1);
  }

  selectAmount(amount: number, type: 'full' | 'min'): void {
    this.selectedAmount.set(amount);
    this.paymentType.set(type);
  }

  selectCard(card: CreditCard): void {
    this.selectedCard.set(card);
  }

  nextStep(): void {
    this.paymentStep.update(s => s + 1);
  }

  initiatePayment(): void {
    if (!this.selectedBill() || !this.selectedCard()) return;
    
    this.isProcessing.set(true);
    this.errorMessage.set(null);

    this.paymentService.initiatePayment({
      cardId: this.selectedCard()!.id,
      billId: this.selectedBill()!.id,
      amount: this.selectedAmount(),
      paymentType: this.paymentType() === 'full' ? 'Full' : 'Minimum'
    }).subscribe({
      next: (res) => {
        this.isProcessing.set(false);
        if (res.success && res.data) {
          this.paymentId.set(res.data.paymentId);
          this.devOtp.set(res.data.devOtp || null);
          this.showPaymentModal.set(false);
          this.showOtpModal.set(true);
        } else {
          this.errorMessage.set(res.message || 'Payment initiation failed');
        }
      },
      error: (err) => {
        this.isProcessing.set(false);
        this.errorMessage.set(err?.error?.message || 'Server error');
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
          this.paidAmount.set(this.selectedAmount());
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
    this.activeTab.set('history');
    this.resetState();
  }

  resetState(): void {
    this.selectedBill.set(null);
    this.selectedCard.set(null);
    this.selectedAmount.set(0);
    this.paymentType.set('full');
    this.otpCode = '';
    this.paymentId.set(null);
    this.devOtp.set(null);
    this.paymentStep.set(1);
  }

  viewBillDetail(bill: Bill): void {
    this.selectedBillDetail.set(bill);
    this.showBillDetailModal.set(true);
  }

  closeBillDetail(): void {
    this.showBillDetailModal.set(false);
    this.selectedBillDetail.set(null);
  }

  viewPaymentDetail(payment: Payment): void {
    this.selectedPaymentDetail.set(payment);
    this.showPaymentDetailModal.set(true);
  }

  closePaymentDetail(): void {
    this.showPaymentDetailModal.set(false);
    this.selectedPaymentDetail.set(null);
  }

  getBillStatusLabel(status: number): string {
    const map: Record<number, string> = { 1: 'Pending', 2: 'Paid', 3: 'Overdue', 4: 'Partially Paid', 5: 'Cancelled' };
    return map[status] || 'Unknown';
  }

  getBillStatusClass(status: number): string {
    const map: Record<number, string> = {
      1: 'bg-amber-100 text-amber-700',
      2: 'bg-green-100 text-green-700',
      3: 'bg-red-100 text-red-700',
      4: 'bg-blue-100 text-blue-700',
      5: 'bg-slate-100 text-slate-600'
    };
    return map[status] || 'bg-slate-100 text-slate-600';
  }
}
