import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { RewardTier } from '../../../core/services/rewards.service';
import { CardIssuer } from '../../../core/models/card.models';

@Component({
  selector: 'app-reward-tiers',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './reward-tiers.component.html'
})
export class RewardTiersComponent implements OnInit {
  private adminService = inject(AdminService);
  private fb = inject(FormBuilder);
  
  tiers = signal<RewardTier[]>([]);
  issuers = signal<CardIssuer[]>([]);
  isLoading = signal(true);
  isSubmitting = signal(false);
  showModal = signal(false);
  isEditMode = signal(false);
  editingTier = signal<RewardTier | null>(null);
  
  showDeleteConfirm = signal(false);
  deleteTargetId = signal<string | null>(null);
  deleteTargetName = signal<string>('');

  successMessage = signal<string | null>(null);
  errorMessage = signal<string | null>(null);

  tierForm = this.fb.group({
    cardNetwork: [1 as number | null, [Validators.required]],
    issuerId: [null as string | null, []],
    minSpend: [0 as number | null, [Validators.required, Validators.min(0)]],
    rewardRate: [0.01 as number | null, [Validators.required, Validators.min(0), Validators.max(1)]],
    effectiveFromUtc: [new Date().toISOString().split('T')[0] as string | null, [Validators.required]],
    effectiveToUtc: [null as string | null, []]
  });

  ngOnInit() {
    this.fetchTiers();
    this.fetchIssuers();
  }

  fetchTiers() {
    this.isLoading.set(true);
    this.adminService.getRewardTiers().subscribe({
      next: (res) => {
        this.tiers.set(res.data || []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Failed to load reward tiers');
      }
    });
  }

  fetchIssuers() {
    this.adminService.getIssuers().subscribe({
      next: (res) => {
        this.issuers.set(res.data || []);
      },
      error: () => {}
    });
  }

  openModal(tier?: RewardTier) {
    this.successMessage.set(null);
    this.errorMessage.set(null);
    
    if (tier) {
      this.isEditMode.set(true);
      this.editingTier.set(tier);
      this.tierForm.patchValue({
        cardNetwork: tier.cardNetwork,
        issuerId: tier.issuerId || null,
        minSpend: tier.minSpend,
        rewardRate: tier.rewardRate,
        effectiveFromUtc: tier.effectiveFromUtc?.split('T')[0] || new Date().toISOString().split('T')[0],
        effectiveToUtc: tier.effectiveToUtc?.split('T')[0] || null
      });
    } else {
      this.isEditMode.set(false);
      this.editingTier.set(null);
      this.tierForm.reset({ cardNetwork: 1, minSpend: 0, rewardRate: 0.01, effectiveFromUtc: new Date().toISOString().split('T')[0] });
    }
    this.showModal.set(true);
  }

  closeModal() {
    this.showModal.set(false);
    this.isEditMode.set(false);
    this.editingTier.set(null);
    this.tierForm.reset({ cardNetwork: 1, minSpend: 0, rewardRate: 0.01, effectiveFromUtc: new Date().toISOString().split('T')[0] });
  }

  onSubmit() {
    if (this.tierForm.invalid) return;
    
    this.isSubmitting.set(true);
    this.successMessage.set(null);
    this.errorMessage.set(null);
    
    const data = { ...this.tierForm.value };
    if (!data.issuerId) delete data.issuerId;
    
    const save$ = this.isEditMode() && this.editingTier()
      ? this.adminService.updateRewardTier(this.editingTier()!.id, data)
      : this.adminService.createRewardTier(data);

    save$.subscribe({
      next: (res) => {
        if (res.success) {
          this.successMessage.set(this.isEditMode() ? 'Tier updated successfully' : 'Tier created successfully');
          this.fetchTiers();
          setTimeout(() => this.closeModal(), 1500);
        } else {
          this.errorMessage.set(res.message || 'Operation failed');
        }
        this.isSubmitting.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to save reward tier');
        this.isSubmitting.set(false);
      }
    });
  }

  confirmDelete(tier: RewardTier) {
    this.deleteTargetId.set(tier.id);
    this.deleteTargetName.set(`${tier.cardNetwork === 1 ? 'Visa' : 'Mastercard'} - $${tier.minSpend}+`);
    this.showDeleteConfirm.set(true);
  }

  cancelDelete() {
    this.showDeleteConfirm.set(false);
    this.deleteTargetId.set(null);
    this.deleteTargetName.set('');
  }

  deleteTier() {
    const id = this.deleteTargetId();
    if (!id) return;
    
    this.showDeleteConfirm.set(false);
    this.adminService.deleteRewardTier(id).subscribe({
      next: (res) => {
        if (res.success) {
          this.fetchTiers();
        } else {
          this.errorMessage.set('Deletion failed: ' + (res.message || 'Unknown error'));
        }
        this.deleteTargetId.set(null);
        this.deleteTargetName.set('');
      },
      error: () => {
        this.errorMessage.set('Failed to delete tier');
        this.deleteTargetId.set(null);
        this.deleteTargetName.set('');
      }
    });
  }

  getTierStatus(tier: RewardTier): { label: string; color: string } {
    const now = new Date();
    const from = new Date(tier.effectiveFromUtc);
    const to = tier.effectiveToUtc ? new Date(tier.effectiveToUtc) : null;
    
    if (now < from) {
      return { label: 'SCHEDULED', color: 'bg-blue-500/10 text-blue-600 border-blue-500/20' };
    }
    if (to && now > to) {
      return { label: 'EXPIRED', color: 'bg-slate-500/10 text-slate-600 border-slate-500/20' };
    }
    return { label: 'ACTIVE', color: 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20' };
  }

  getNetworkName(network: number): string {
    return network === 1 ? 'Visa' : network === 2 ? 'Mastercard' : 'Unknown';
  }

  formatRewardRate(rate: number): string {
    return (rate * 100).toFixed(1) + '%';
  }

  getCashbackDisplay(rate: number | null | undefined): string {
    if (!rate || rate === 0) return '0%';
    const pct = (rate * 100).toFixed(2).replace(/\.?0+$/, '');
    return pct;
  }
}
