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
    const txs = this.transactions().filter(t => t.type === 1); // Only Purchases
    if (txs.length === 0) return [];

    const categories: Record<string, number> = {};
    let total = 0;

    txs.forEach(t => {
      const desc = t.description.toLowerCase();
      let category = 'Services'; // Default fallback
      
      if (desc.includes('food') || desc.includes('rest') || desc.includes('dine') || desc.includes('cafe')) category = 'Dining';
      else if (desc.includes('shop') || desc.includes('amazon') || desc.includes('store') || desc.includes('market') || desc.includes('mall')) category = 'Shopping';
      else if (desc.includes('travel') || desc.includes('uber') || desc.includes('flight') || desc.includes('hotel') || desc.includes('lyft')) category = 'Travel';
      else if (desc.includes('rent') || desc.includes('mortgage') || desc.includes('lease') || desc.includes('housing')) category = 'Housing';
      else if (desc.includes('electricity') || desc.includes('water') || desc.includes('internet') || desc.includes('bill') || desc.includes('utility')) category = 'Utilities';
      else if (desc.includes('netflix') || desc.includes('spotify') || desc.includes('game') || desc.includes('movie') || desc.includes('hulu')) category = 'Entertainment';

      categories[category] = (categories[category] || 0) + t.amount;
      total += t.amount;
    });

    return Object.entries(categories)
      .map(([name, amount]) => ({
        name,
        amount,
        percentage: Math.round((amount / total) * 100)
      }))
      .sort((a, b) => b.amount - a.amount);
  });

  ngOnInit(): void {
    const cardId = this.route.snapshot.paramMap.get('id');
    if (cardId) {
      this.loadCardDetails(cardId);
      this.loadRewards();
    } else {
      this.error.set('Card ID not found');
      this.isLoading.set(false);
    }
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
    switch (type) {
      case 1: return 'shopping_cart';
      case 2: return 'account_balance_wallet';
      case 3: return 'keyboard_return';
      default: return 'payments';
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
