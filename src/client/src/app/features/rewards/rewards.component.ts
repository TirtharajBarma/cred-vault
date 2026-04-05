import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RewardsService, RewardAccount, RewardTransaction, RewardTier } from '../../core/services/rewards.service';
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
  rewardTiers = signal<RewardTier[]>([]);
  rewardCardLabelMap = signal<Record<string, string>>({});
  issuerMap = signal<Record<string, string>>({});
  isLoading = signal(true);
  error = signal<string | null>(null);

  tiersPage = signal(1);
  tiersPerPage = 3;

  paginatedTiers = computed(() => {
    const start = (this.tiersPage() - 1) * this.tiersPerPage;
    return this.rewardTiers().slice(start, start + this.tiersPerPage);
  });

  totalTiersPages = computed(() => {
    return Math.max(1, Math.ceil(this.rewardTiers().length / this.tiersPerPage));
  });

  nextTiersPage(): void {
    if (this.tiersPage() < this.totalTiersPages()) {
      this.tiersPage.set(this.tiersPage() + 1);
    }
  }

  prevTiersPage(): void {
    if (this.tiersPage() > 1) {
      this.tiersPage.set(this.tiersPage() - 1);
    }
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.error.set(null);

    forkJoin({
      account: this.rewardsService.getRewardAccount(),
      history: this.rewardsService.getRewardHistory(),
      tiers: this.rewardsService.getRewardTiers(),
      cards: this.dashboardService.getCards(),
      bills: this.billingService.getMyBills()
    }).subscribe({
      next: (res) => {
        if (res.account.data) {
          this.rewardAccount.set(res.account.data);
        }
        this.transactions.set(res.history.data || []);
        this.rewardTiers.set(res.tiers.data || []);

        const cards = res.cards.data || [];
        const bills = res.bills.data || [];

        const cardLabelById: Record<string, string> = {};
        const issuerNameById: Record<string, string> = {};
        cards.forEach(card => {
          cardLabelById[card.id] = `${card.issuerName || 'Card'} •••• ${card.last4}`;
          if (card.issuerId && card.issuerName) {
            issuerNameById[card.issuerId] = card.issuerName;
          }
        });

        const map: Record<string, string> = {};
        bills.forEach(bill => {
          map[bill.id] = cardLabelById[bill.cardId] || 'Card';
        });

        this.issuerMap.set(issuerNameById);
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