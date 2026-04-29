import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { WalletService, WalletTransaction, RazorpayTopUpOrder } from '../../../core/services/wallet.service';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { environment } from '../../../../environments/environment';

declare global {
  interface Window {
    Razorpay?: new (options: Record<string, unknown>) => { open: () => void };
  }
}

@Component({
  selector: 'app-wallet-activity',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, IstDatePipe],
  templateUrl: './wallet-activity.component.html',
  styleUrl: './wallet-activity.component.css'
})
export class WalletActivityComponent implements OnInit {
  private walletService = inject(WalletService);
  private razorpayLoader?: Promise<void>;

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

  async submitTopUp(): Promise<void> {
    const amount = this.topUpAmount();

    if (!amount || amount <= 0) {
      this.topUpError.set('Please enter an amount greater than zero.');
      return;
    }

    this.isTopUpSubmitting.set(true);
    this.topUpError.set(null);
    this.topUpSuccess.set(null);

    try {
      if (!environment.razorpayKeyId) {
        throw new Error('Razorpay public key is missing in frontend configuration.');
      }

      await this.ensureRazorpayLoaded();
      const createOrderRes = await firstValueFrom(
        this.walletService.createTopUpOrder({ amount, description: this.topUpDescription() || 'Wallet top-up' })
      );

      if (!createOrderRes.success || !createOrderRes.data) {
        throw new Error(createOrderRes.message || 'Unable to create payment order.');
      }

      const checkoutResponse = await this.openRazorpayCheckout(createOrderRes.data);
      const verifyRes = await firstValueFrom(this.walletService.verifyTopUp({
        topUpId: createOrderRes.data.topUpId,
        razorpayOrderId: checkoutResponse.razorpay_order_id,
        razorpayPaymentId: checkoutResponse.razorpay_payment_id,
        razorpaySignature: checkoutResponse.razorpay_signature
      }));

      if (!verifyRes.success) {
        throw new Error(verifyRes.message || 'Unable to verify payment.');
      }

      const newBalance = Number(verifyRes.data?.newBalance || 0);
      this.walletBalance.set(newBalance);
      this.topUpSuccess.set(
        `Successfully topped up ₹${amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}. New balance: ₹${newBalance.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
      );
      this.loadWalletData();
      setTimeout(() => this.toggleTopUpModal(), 1600);
    } catch (err: any) {
      this.topUpError.set(err?.message || err?.error?.message || 'Unable to top up wallet right now.');
    } finally {
      this.isTopUpSubmitting.set(false);
    }
  }

  private async ensureRazorpayLoaded(): Promise<void> {
    if (typeof window === 'undefined') {
      throw new Error('Razorpay checkout is only available in the browser.');
    }

    if (window.Razorpay) {
      return;
    }

    if (this.razorpayLoader) {
      return this.razorpayLoader;
    }

    this.razorpayLoader = new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = 'https://checkout.razorpay.com/v1/checkout.js';
      script.async = true;
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('Unable to load Razorpay checkout.'));
      document.body.appendChild(script);
    });

    return this.razorpayLoader;
  }

  private openRazorpayCheckout(order: RazorpayTopUpOrder): Promise<{
    razorpay_order_id: string;
    razorpay_payment_id: string;
    razorpay_signature: string;
  }> {
    return new Promise((resolve, reject) => {
      if (!window.Razorpay) {
        reject(new Error('Razorpay checkout is unavailable.'));
        return;
      }

      const razorpay = new window.Razorpay({
        key: order.keyId || environment.razorpayKeyId,
        amount: order.amount,
        currency: order.currency,
        name: order.displayName || 'CredVault',
        description: order.description || 'Wallet top-up',
        order_id: order.orderId,
        handler: (response: {
          razorpay_order_id: string;
          razorpay_payment_id: string;
          razorpay_signature: string;
        }) => resolve(response),
        modal: {
          ondismiss: () => reject(new Error('Payment was cancelled.'))
        },
        theme: {
          color: '#8a5100'
        }
      });

      razorpay.open();
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
