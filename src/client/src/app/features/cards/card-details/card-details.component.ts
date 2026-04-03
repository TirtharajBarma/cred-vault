import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DashboardService } from '../../../core/services/dashboard.service';
import { CreditCard, CardTransaction } from '../../../core/models/card.models';
import { ApiResponse } from '../../../core/models/auth.models';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-card-details',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './card-details.component.html',
  styleUrl: './card-details.component.css'
})
export class CardDetailsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private dashboardService = inject(DashboardService);

  card = signal<CreditCard | null>(null);
  transactions = signal<CardTransaction[]>([]);
  rewards = signal<number>(0);
  isLoading = signal(true);
  error = signal<string | null>(null);

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
    if (!card || card.outstandingBalance <= 0) return [];

    const txs = this.currentCardTransactions();
    if (txs.length === 0) return [];

    const categories: Record<string, number> = {};
    let remainingOutstanding = card.outstandingBalance;
    let total = 0;

    // Reconstruct outstanding composition from newest to oldest transactions.
    for (const tx of txs) {
      const normalizedType = this.getNormalizedTransactionType(tx.type);

      if (normalizedType === 1) {
        if (remainingOutstanding <= 0) continue;

        const allocated = Math.min(tx.amount, remainingOutstanding);
        const desc = (tx.description || '').toLowerCase();
        let category = 'Services';

        if (desc.includes('food') || desc.includes('rest') || desc.includes('dine') || desc.includes('cafe')) category = 'Dining';
        else if (desc.includes('shop') || desc.includes('amazon') || desc.includes('store') || desc.includes('market') || desc.includes('mall')) category = 'Shopping';
        else if (desc.includes('travel') || desc.includes('uber') || desc.includes('flight') || desc.includes('hotel') || desc.includes('lyft')) category = 'Travel';
        else if (desc.includes('rent') || desc.includes('mortgage') || desc.includes('lease') || desc.includes('housing')) category = 'Housing';
        else if (desc.includes('electricity') || desc.includes('water') || desc.includes('internet') || desc.includes('bill') || desc.includes('utility')) category = 'Utilities';
        else if (desc.includes('netflix') || desc.includes('spotify') || desc.includes('game') || desc.includes('movie') || desc.includes('hulu')) category = 'Entertainment';

        categories[category] = (categories[category] || 0) + allocated;
        total += allocated;
        remainingOutstanding -= allocated;
      } else {
        // Going backwards in time, payments/refunds increase pre-payment outstanding.
        remainingOutstanding += tx.amount;
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
      (a, b) => new Date(b.dateUtc).getTime() - new Date(a.dateUtc).getTime()
    );
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const cardId = params.get('id');
      if (cardId) {
        this.loadCardDetails(cardId);
        this.loadRewards();
      } else {
        this.error.set('Card ID not found');
        this.isLoading.set(false);
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
    this.dashboardService.getRewardAccount().subscribe({
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
      type: 1, // Purchase
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
      error: (err: any) => this.submitError.set('Network error. Is the backend running?')
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

  getTransactionIcon(type: number): string {
    switch (this.getNormalizedTransactionType(type)) {
      case 1: return 'shopping_cart';
      case 2: return 'account_balance_wallet';
      case 3: return 'keyboard_return';
      default: return 'payments';
    }
  }

  getNormalizedTransactionType(type: number | string): 1 | 2 | 3 {
    if (type === 1 || type === '1' || type === 'Purchase') return 1;
    if (type === 2 || type === '2' || type === 'Payment') return 2;
    if (type === 3 || type === '3' || type === 'Refund') return 3;
    return 1;
  }

  getTransactionTitle(tx: CardTransaction): string {
    const description = (tx.description || '').trim();
    const lowered = description.toLowerCase();

    if (lowered.startsWith('bill payment:')) {
      return `Bill Statement of ${this.getMonthYear(tx.dateUtc)}`;
    }

    if (lowered.startsWith('saga:') && this.getNormalizedTransactionType(tx.type) === 2) {
      return `Bill Statement of ${this.getMonthYear(tx.dateUtc)}`;
    }

    return description || 'Card Transaction';
  }

  getTransactionFlowLabel(type: number | string): string {
    return this.getNormalizedTransactionType(type) === 1 ? 'Debit' : 'Credit';
  }

  getTransactionTypeLabel(type: number | string): string {
    const normalized = this.getNormalizedTransactionType(type);
    if (normalized === 1) return 'Purchase';
    if (normalized === 2) return 'Payment';
    return 'Refund';
  }

  getSignedAmountPrefix(type: number | string): string {
    return this.getNormalizedTransactionType(type) === 1 ? '-' : '+';
  }

  private getMonthYear(dateUtc: string): string {
    return new Intl.DateTimeFormat('en-IN', {
      month: 'short',
      year: 'numeric'
    }).format(new Date(dateUtc));
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
}
