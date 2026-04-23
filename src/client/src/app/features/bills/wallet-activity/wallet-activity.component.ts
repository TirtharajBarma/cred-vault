import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { WalletService, WalletTransaction } from '../../../core/services/wallet.service';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'app-wallet-activity',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, IstDatePipe],
  templateUrl: './wallet-activity.component.html',
  styleUrl: './wallet-activity.component.css'
})
export class WalletActivityComponent implements OnInit {
  private walletService = inject(WalletService);

  isLoading = signal(true);
  hasWallet = signal(false);
  walletBalance = signal(0);
  transactions = signal<WalletTransaction[]>([]);

  showTopUpModal = signal(false);
  topUpAmount = signal<number>(0);
  topUpDescription = signal('');
  isTopUpSubmitting = signal(false);
  topUpError = signal<string | null>(null);
  topUpSuccess = signal<string | null>(null);

  ngOnInit(): void {
    this.loadWalletData();
  }

  loadWalletData(): void {
    this.isLoading.set(true);

    this.walletService.getBalance().subscribe({
      next: (balanceRes) => {
        if (balanceRes.success && balanceRes.data) {
          this.walletBalance.set(Number(balanceRes.data.balance || 0));
          this.hasWallet.set(Boolean(balanceRes.data.hasWallet));
        }

        this.walletService.getTransactions(0, 100).subscribe({
          next: (txRes) => {
            this.transactions.set(txRes.success ? txRes.data || [] : []);
            this.isLoading.set(false);
          },
          error: () => {
            this.transactions.set([]);
            this.isLoading.set(false);
          }
        });
      },
      error: () => {
        this.walletBalance.set(0);
        this.hasWallet.set(false);
        this.transactions.set([]);
        this.isLoading.set(false);
      }
    });
  }

  toggleTopUpModal(): void {
    this.showTopUpModal.set(!this.showTopUpModal());
    this.topUpAmount.set(0);
    this.topUpDescription.set('');
    this.topUpError.set(null);
    this.topUpSuccess.set(null);
  }

  submitTopUp(): void {
    const amount = this.topUpAmount();

    if (!amount || amount <= 0) {
      this.topUpError.set('Please enter an amount greater than zero.');
      return;
    }

    this.isTopUpSubmitting.set(true);
    this.topUpError.set(null);
    this.topUpSuccess.set(null);

    this.walletService.topUp({ amount, description: this.topUpDescription() || 'Wallet top-up' }).subscribe({
      next: (res) => {
        if (res.success) {
          const newBalance = Number(res.data?.newBalance || 0);
          this.walletBalance.set(newBalance);
          this.topUpSuccess.set(`Successfully topped up ₹${amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}. New balance: ₹${newBalance.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`);
          this.loadWalletData();
          setTimeout(() => this.toggleTopUpModal(), 1600);
        } else {
          this.topUpError.set(res.message || 'Unable to top up wallet right now.');
        }
        this.isTopUpSubmitting.set(false);
      },
      error: (err) => {
        this.topUpError.set(err?.error?.message || 'Unable to top up wallet right now.');
        this.isTopUpSubmitting.set(false);
      }
    });
  }

  getTransactionTypeName(type: number): string {
    switch (type) {
      case 1: return 'Top Up';
      case 2: return 'Bill Payment';
      case 3: return 'Refund';
      case 4: return 'Withdrawal';
      default: return 'Transaction';
    }
  }

  getTransactionIcon(type: number): string {
    switch (type) {
      case 1: return 'add_circle';
      case 2: return 'payments';
      case 3: return 'settings_backup_restore';
      case 4: return 'send';
      default: return 'receipt';
    }
  }

  getTransactionClass(type: number): string {
    switch (type) {
      case 1: return 'text-green-600';
      case 2: return 'text-[#8a5100]';
      case 3: return 'text-green-600';
      case 4: return 'text-red-500';
      default: return 'text-[#615e5c]';
    }
  }

  getSignedAmount(type: number, amount: number): string {
    if (type === 1 || type === 3) return `+${amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    if (type === 2 || type === 4) return `-${amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    return amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
}
