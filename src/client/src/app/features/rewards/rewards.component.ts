import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { catchError, forkJoin, of } from 'rxjs';
import { RewardsService, RewardAccount, RewardTransaction, RewardTier } from '../../core/services/rewards.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { AdminService } from '../../core/services/admin.service';
import { BillingService } from '../../core/services/billing.service';

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
  private adminService = inject(AdminService);
  private billingService = inject(BillingService);

  Math = Math;

  rewardAccount = signal<RewardAccount | null>(null);
  transactions = signal<RewardTransaction[]>([]);
  rewardTiers = signal<RewardTier[]>([]);
  rewardCardLabelMap = signal<Record<string, string>>({});
  issuerMap = signal<Record<string, string>>({});
  brokenNetworkLogos = signal<Record<string, true>>({});
  isLoading = signal(true);
  error = signal<string | null>(null);

  tiersPage = signal(1);
  tiersPerPage = 3;

  transactionsPage = signal(1);
  transactionsPerPage = 7;

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
      account: this.rewardsService.getRewardAccount().pipe(
        catchError(() => of({ success: false, message: 'Failed to load account', data: null, traceId: '' }))
      ),
      history: this.rewardsService.getRewardHistory().pipe(
        catchError(() => of({ success: false, message: 'Failed to load history', data: [], traceId: '' }))
      ),
      tiers: this.rewardsService.getRewardTiers().pipe(
        catchError(() => of({ success: false, message: 'Failed to load tiers', data: [], traceId: '' }))
      ),
      cards: this.dashboardService.getCards().pipe(
        catchError(() => of({ success: false, message: 'Failed to load cards', data: [], traceId: '' }))
      ),
      bills: this.billingService.getMyBills().pipe(
        catchError(() => of({ success: false, message: 'Failed to load bills', data: [], traceId: '' }))
      ),
      issuers: this.adminService.getIssuers().pipe(
        catchError(() => of({ success: false, message: 'Failed to load issuers', data: [], traceId: '' }))
      )
    }).subscribe({
      next: (res) => {
        if (res.account.success && res.account.data) {
          this.rewardAccount.set(res.account.data);
        }

        this.transactions.set(res.history.data || []);
        this.rewardTiers.set(res.tiers.data || []);

        const cards = res.cards.data || [];
        const bills = res.bills.data || [];
        const issuers = res.issuers.data || [];

        const cardLabelById: Record<string, string> = {};
        const issuerNameById: Record<string, string> = {};

        issuers.forEach(issuer => {
          if (issuer.id && issuer.name) {
            issuerNameById[issuer.id] = issuer.name;
          }
        });

        cards.forEach(card => {
          cardLabelById[card.id] = `${card.issuerName || 'Card'} \u2022\u2022\u2022\u2022 ${card.last4}`;
          if (card.issuerId && card.issuerName && !issuerNameById[card.issuerId]) {
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
      error: () => {
        this.error.set('Failed to load rewards data');
        this.isLoading.set(false);
      }
    });
  }

  refresh(): void {
    this.loadData();
  }

  sortedTransactions() {
    return [...this.transactions()].sort(
      (a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()
    );
  }

  paginatedTransactions() {
    const sorted = this.sortedTransactions();
    const start = (this.transactionsPage() - 1) * this.transactionsPerPage;
    return sorted.slice(start, start + this.transactionsPerPage);
  }

  totalTransactionPages(): number {
    return Math.ceil(this.transactions().length / this.transactionsPerPage);
  }

  nextTransactionPage(): void {
    if (this.transactionsPage() < this.totalTransactionPages()) {
      this.transactionsPage.set(this.transactionsPage() + 1);
    }
  }

  prevTransactionPage(): void {
    if (this.transactionsPage() > 1) {
      this.transactionsPage.set(this.transactionsPage() - 1);
    }
  }

  getRewardActivityLabel(tx: RewardTransaction): string {
    const cardLabel = this.rewardCardLabelMap()[tx.billId];
    if (!cardLabel) return 'Bill Payment Reward';
    return `Bill Payment Reward - ${cardLabel}`;
  }

  getTransactionTypeLabel(tx: RewardTransaction): { label: string; cssClass: string } {
    if (tx.reversedAtUtc) {
      return { label: 'Reversed', cssClass: 'bg-gray-500/10 text-gray-500' };
    }
    switch (tx.type) {
      case 1: return { label: 'Earned', cssClass: 'bg-emerald-500/10 text-emerald-600' };
      case 2: return { label: 'Adjusted', cssClass: 'bg-blue-500/10 text-blue-600' };
      case 3: return { label: 'Redeemed', cssClass: 'bg-red-500/10 text-red-600' };
      case 4: return { label: 'Reversed', cssClass: 'bg-gray-500/10 text-gray-500' };
      default: return { label: 'Unknown', cssClass: 'bg-gray-500/10 text-gray-500' };
    }
  }

  getTransactionPointsDisplay(tx: RewardTransaction): string {
    const prefix = (tx.type === 3 || tx.type === 4 || tx.reversedAtUtc) ? '-' : '';
    return `${prefix}${tx.points}`;
  }

  getIssuerName(tier: RewardTier): string {
    if (tier.issuerId) {
      return this.issuerMap()[tier.issuerId] || 'Partner Bank';
    }
    const networkName = this.getNetworkName(tier);
    return `All Banks on ${networkName}`;
  }

  getRewardRateDisplay(tier: RewardTier): string {
    const pct = (tier.rewardRate * 100).toFixed(2).replace(/\.?0+$/, '');
    return `${pct}%`;
  }

  getRewardRateDescription(tier: RewardTier): string {
    const pct = (tier.rewardRate * 100).toFixed(1).replace(/\.?0+$/, '');
    return `Earn ${pct}% cashback on every eligible bill payment`;
  }

  getMinSpendDisplay(minSpend: number): string {
    if (minSpend === 0) return 'No minimum';
    if (minSpend >= 1000) {
      const k = minSpend / 1000;
      return `₹${k % 1 === 0 ? k : k.toFixed(1)}+`;
    }
    return `₹${minSpend}+`;
  }

  getNetworkBadgeClass(tier: RewardTier): string {
    switch (tier.cardNetwork) {
      case 2: return 'bg-[#f0a046]/20 text-[#653a00]';
      case 1: return 'bg-stone-100 text-stone-600 border border-stone-200';
      default: return 'bg-gray-100 text-gray-600 border border-gray-200';
    }
  }

  getNetworkIcon(tier: RewardTier): string {
    switch (tier.cardNetwork) {
      case 2: return 'token';
      case 1: return 'payments';
      default: return 'credit_card';
    }
  }

  getNetworkIconColor(tier: RewardTier): string {
    switch (tier.cardNetwork) {
      case 2: return 'text-orange-600';
      case 1: return 'text-blue-800';
      default: return 'text-gray-500';
    }
  }

  getNetworkLogoSrc(tier: RewardTier): string | null {
    switch (tier.cardNetwork) {
      case 1: return '/assets/visa.png';
      case 2: return '/assets/mastercard.png';
      default: return null;
    }
  }

  hasUsableNetworkLogo(tier: RewardTier): boolean {
    const logoSrc = this.getNetworkLogoSrc(tier);
    return !!logoSrc && !this.brokenNetworkLogos()[logoSrc];
  }

  onNetworkLogoError(tier: RewardTier): void {
    const logoSrc = this.getNetworkLogoSrc(tier);
    if (!logoSrc) return;
    this.brokenNetworkLogos.update((current) => ({ ...current, [logoSrc]: true }));
  }

  getNetworkName(tier: RewardTier): string {
    switch (tier.cardNetwork) {
      case 1: return 'Visa';
      case 2: return 'Mastercard';
      default: return 'Unknown Network';
    }
  }

  isCurrentTier(tier: RewardTier): boolean {
    const account = this.rewardAccount();
    return account !== null && account.rewardTierId === tier.id;
  }

  getActiveRateInfo(): { tier: RewardTier | null; rate: string; description: string } {
    const account = this.rewardAccount();
    if (!account) return { tier: null, rate: '\u2014', description: 'No active reward tier' };
    const tiers = this.rewardTiers();
    const activeTier = tiers.find(t => t.id === account.rewardTierId);
    if (!activeTier) return { tier: null, rate: '\u2014', description: 'No active reward tier' };
    const rate = this.getRewardRateDisplay(activeTier);
    const description = this.getRewardRateDescription(activeTier);
    return { tier: activeTier, rate, description };
  }
}
