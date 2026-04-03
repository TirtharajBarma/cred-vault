import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
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
  private route = inject(ActivatedRoute);

  activeTab = signal<'bills' | 'history'>('bills');
  bills = signal<Bill[]>([]);
  payments = signal<Payment[]>([]);
  cards = signal<CreditCard[]>([]);
  isLoading = signal(true);

  currentPage = signal(1);
  itemsPerPage = 7;

  showPaymentModal = signal(false);
  selectedBill = signal<Bill | null>(null);
  paymentType = signal<'full' | 'min'>('full');
  
  isProcessing = signal(false);
  errorMessage = signal<string | null>(null);

  showOtpModal = signal(false);
  showSuccessModal = signal(false);
  showBillDetailModal = signal(false);
  showPaymentDetailModal = signal(false);
  selectedBillDetail = signal<Bill | null>(null);
  selectedPaymentDetail = signal<Payment | null>(null);
  paymentId = signal<string | null>(null);
  
  otpCode = '';
  isVerifying = signal(false);
  otpError = signal<string | null>(null);
  otpInfo = signal<string | null>(null);
  canResendOtp = signal(true);
  isResendingOtp = signal(false);
  paidAmount = signal(0);

  ngOnInit(): void {
    this.loadData();
    
    // Handle billId query parameter
    this.route.queryParams.subscribe(params => {
      if (params['billId']) {
        const billId = params['billId'];
        setTimeout(() => {
          const bill = this.bills().find(b => b.id === billId);
          if (bill) {
            this.openPayment(bill);
          }
        }, 500);
      }
    });
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
    return this.bills().filter(b => b.status === 1 || b.status === 3);
  }

  paginatedBills(): Bill[] {
    const pending = this.pendingBills();
    const start = (this.currentPage() - 1) * this.itemsPerPage;
    return pending.slice(start, start + this.itemsPerPage);
  }

  paginatedPayments(): Payment[] {
    const start = (this.currentPage() - 1) * this.itemsPerPage;
    return this.payments().slice(start, start + this.itemsPerPage);
  }

  totalBillPages(): number {
    return Math.ceil(this.pendingBills().length / this.itemsPerPage);
  }

  totalPaymentPages(): number {
    return Math.ceil(this.payments().length / this.itemsPerPage);
  }

  nextPage(): void {
    const total = this.activeTab() === 'bills' ? this.totalBillPages() : this.totalPaymentPages();
    if (this.currentPage() < total) {
      this.currentPage.set(this.currentPage() + 1);
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.set(this.currentPage() - 1);
    }
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

  openPayment(bill: Bill): void {
    this.selectedBill.set(bill);
    this.paymentType.set('full');
    this.errorMessage.set(null);
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
    
    const amount = this.paymentType() === 'full' ? bill.amount : bill.minDue;
    
    this.isProcessing.set(true);
    this.errorMessage.set(null);

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
    this.otpInfo.set(null);

    this.paymentService.verifyOtp(this.paymentId()!, this.otpCode).subscribe({
      next: (res) => {
        this.isVerifying.set(false);
        if (res.success) {
          this.showOtpModal.set(false);
          this.showSuccessModal.set(true);
          this.paidAmount.set(this.paymentType() === 'full' ? this.selectedBill()!.amount : this.selectedBill()!.minDue);
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

  resendOtp(): void {
    const currentPaymentId = this.paymentId();
    if (!this.canResendOtp() || !currentPaymentId || this.isResendingOtp()) return;

    this.isResendingOtp.set(true);
    this.otpError.set(null);
    this.otpInfo.set(null);

    this.paymentService.resendOtp(currentPaymentId).subscribe({
      next: (res) => {
        this.isResendingOtp.set(false);
        if (res.success) {
          this.otpInfo.set(res.message || 'A fresh OTP has been sent to your registered email.');
          this.canResendOtp.set(false);
          setTimeout(() => this.canResendOtp.set(true), 30000);
          return;
        }

        this.otpError.set(res.message || 'Failed to resend OTP.');
      },
      error: (err) => {
        this.isResendingOtp.set(false);
        this.otpError.set(err?.error?.message || 'Failed to resend OTP.');
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
    this.paymentType.set('full');
    this.otpCode = '';
    this.paymentId.set(null);
    this.otpError.set(null);
    this.otpInfo.set(null);
    this.canResendOtp.set(true);
    this.isResendingOtp.set(false);
    this.paymentType.set('full');
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
}
