import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RewardsService, RewardAccount, RewardTransaction, RewardTier } from '../../core/services/rewards.service';

@Component({
  selector: 'app-rewards',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './rewards.component.html',
  styleUrls: ['./rewards.component.css']
})
export class RewardsComponent implements OnInit {
  private rewardsService = inject(RewardsService);

  rewardAccount = signal<RewardAccount | null>(null);
  tiers = signal<RewardTier[]>([]);
  isLoading = signal(true);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.rewardsService.getRewardAccount().subscribe({
      next: (res) => {
        if (res.data) this.rewardAccount.set(res.data);
        this.rewardsService.getRewardTiers().subscribe({
          next: (res) => {
            this.tiers.set(res.data || []);
            this.isLoading.set(false);
          },
          error: () => this.isLoading.set(false)
        });
      },
      error: () => this.isLoading.set(false)
    });
  }

  getNetworkName(network: number): string {
    return network === 1 ? 'Visa' : network === 2 ? 'Mastercard' : 'Unknown';
  }

  getTierStatus(tier: RewardTier): { label: string; class: string } {
    const now = new Date();
    const from = new Date(tier.effectiveFromUtc);
    const to = tier.effectiveToUtc ? new Date(tier.effectiveToUtc) : null;
    if (now < from) return { label: 'Scheduled', class: 'bg-blue-100 text-blue-700' };
    if (to && now > to) return { label: 'Expired', class: 'bg-slate-100 text-slate-600' };
    return { label: 'Active', class: 'bg-green-100 text-green-700' };
  }
}
