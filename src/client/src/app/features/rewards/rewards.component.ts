import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RewardsService, RewardAccount, RewardTransaction } from '../../core/services/rewards.service';
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

  rewardAccount = signal<RewardAccount | null>(null);
  transactions = signal<RewardTransaction[]>([]);
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
      history: this.rewardsService.getRewardHistory()
    }).subscribe({
      next: (res) => {
        if (res.account.data) {
          this.rewardAccount.set(res.account.data);
        }
        this.transactions.set(res.history.data || []);
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
}