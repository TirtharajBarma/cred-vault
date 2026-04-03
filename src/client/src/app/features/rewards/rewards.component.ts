import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RewardsService, RewardAccount, RewardTransaction } from '../../core/services/rewards.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { BillingService } from '../../core/services/billing.service';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-rewards',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './rewards.component.html',
  styleUrls: ['./rewards.component.css']
})
export class RewardsComponent implements OnInit {
  private rewardsService = inject(RewardsService);
  private dashboardService = inject(DashboardService);
  private billingService = inject(BillingService);

  rewardAccount = signal<RewardAccount | null>(null);
  transactions = signal<RewardTransaction[]>([]);
  rewardCardLabelMap = signal<Record<string, string>>({});
  isLoading = signal(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.error.set(null);

    forkJoin({
      account: this.rewardsService.getRewardAccount(),
      history: this.rewardsService.getRewardHistory(),
      cards: this.dashboardService.getCards(),
      bills: this.billingService.getMyBills()
    }).subscribe({
      next: (res) => {
        if (res.account.data) {
          this.rewardAccount.set(res.account.data);
        }
        this.transactions.set(res.history.data || []);

        const cards = res.cards.data || [];
        const bills = res.bills.data || [];

        const cardLabelById: Record<string, string> = {};
        cards.forEach(card => {
          cardLabelById[card.id] = `${card.issuerName || 'Card'} •••• ${card.last4}`;
        });

        const map: Record<string, string> = {};
        bills.forEach(bill => {
          map[bill.id] = cardLabelById[bill.cardId] || 'Card';
        });

        this.rewardCardLabelMap.set(map);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading rewards data:', err);
        this.error.set('Failed to load rewards data');
        this.isLoading.set(false);
      }
    });
  }

  refresh(): void {
    this.loadData();
  }

  getRewardActivityLabel(tx: RewardTransaction): string {
    const cardLabel = this.rewardCardLabelMap()[tx.billId];
    if (!cardLabel) return 'Bill Payment Reward';
    return `Bill Payment Reward - ${cardLabel}`;
  }
}