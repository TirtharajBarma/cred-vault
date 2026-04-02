import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { BillingService, Bill, BillStatus } from '../../core/services/billing.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { PaymentService } from '../../core/services/payment.service';
import { CreditCard } from '../../core/models/card.models';

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
  filterStatus = signal<number | null>(null);
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
      .filter(b => b.status === BillStatus.Pending)
      .reduce((acc, b) => acc + b.amount, 0);
  });

  upcomingMilestone = computed(() => {
    const pending = this.bills()
      .filter(b => b.status === BillStatus.Pending)
      .sort((a, b) => new Date(a.dueDateUtc).getTime() - new Date(b.dueDateUtc).getTime());
    return pending.length > 0 ? pending[0] : null;
  });

  filteredBills() {
    const status = this.filterStatus();
    if (status === null) return this.bills();
    return this.bills().filter(b => b.status === status);
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
    const diff = new Date(date).getTime() - new Date().getTime();
    const days = Math.ceil(diff / (1000 * 60 * 60 * 24));
    if (days <= 0) return 'DUE TODAY';
    if (days === 1) return 'DUE IN 1 DAY';
    return `DUE IN ${days} DAYS`;
  }

  getCountdownLabel(date: string): string {
    const diff = new Date(date).getTime() - new Date().getTime();
    const days = Math.ceil(diff / (1000 * 60 * 60 * 24));
    if (days <= 0) return 'today';
    if (days === 1) return 'in 1 day';
    return `in ${days} days`;
  }

  getStatusLabel(status: number): string {
    switch (status) {
      case BillStatus.Pending: return 'Pending';
      case BillStatus.Paid: return 'Paid';
      case BillStatus.Overdue: return 'Overdue';
      case BillStatus.PartiallyPaid: return 'Partial';
      default: return 'Unknown';
    }
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
    
    const amount = this.paymentType() === 'full' ? bill.amount : bill.minDue;
    
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

  setFilter(status: number | null): void {
    this.filterStatus.set(status);
    this.currentPage.set(1);
  }
}