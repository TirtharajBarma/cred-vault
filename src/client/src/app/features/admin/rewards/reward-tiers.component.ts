import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { RewardTier } from '../../../core/services/rewards.service';
import { CardIssuer } from '../../../core/models/card.models';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { parseUtcDate } from '../../../core/utils/date-time.util';

@Component({
  selector: 'app-reward-tiers',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, IstDatePipe],
  templateUrl: './reward-tiers.component.html'
})
export class RewardTiersComponent implements OnInit {
  private adminService = inject(AdminService);
  private fb = inject(FormBuilder);

  tiers = signal<RewardTier[]>([]);
  issuers = signal<CardIssuer[]>([]);
  brokenNetworkLogos = signal<Record<string, true>>({});
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
  isDeleting = signal(false);
  deleteErrorMessage = signal<string | null>(null);

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
      this.tierForm.reset({ cardNetwork: 1, issuerId: null, minSpend: 0, rewardRate: 0.01, effectiveFromUtc: new Date().toISOString().split('T')[0] });
    }
    this.showModal.set(true);
  }

  closeModal() {
    this.showModal.set(false);
    this.isEditMode.set(false);
    this.editingTier.set(null);
    this.tierForm.reset({ cardNetwork: 1, issuerId: null, minSpend: 0, rewardRate: 0.01, effectiveFromUtc: new Date().toISOString().split('T')[0] });
  }

  onSubmit() {
    if (this.tierForm.invalid) return;

    this.isSubmitting.set(true);
    this.successMessage.set(null);
    this.errorMessage.set(null);

    const data = { ...this.tierForm.value };

    if (data.issuerId && data.cardNetwork) {
      const selectedIssuer = this.issuers().find((issuer) => issuer.id === data.issuerId);
      if (!selectedIssuer || selectedIssuer.network !== data.cardNetwork) {
        this.errorMessage.set('Please select an issuer that belongs to the selected network.');
        this.isSubmitting.set(false);
        return;
      }
    }

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
    if (!tier?.id || tier.id === 'null' || tier.id === 'undefined') {
      this.errorMessage.set('Unable to delete this tier right now. Please refresh and try again.');
      return;
    }

    this.deleteErrorMessage.set(null);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.deleteTargetId.set(tier.id);
    this.deleteTargetName.set(`${this.getTierBankName(tier)} (${this.getNetworkName(tier.cardNetwork)} • ${this.formatMinSpendForCard(tier.minSpend)})`);
    this.showDeleteConfirm.set(true);
  }

  cancelDelete() {
    if (this.isDeleting()) return;
    this.showDeleteConfirm.set(false);
    this.deleteErrorMessage.set(null);
    this.deleteTargetId.set(null);
    this.deleteTargetName.set('');
  }

  deleteTier() {
    const id = this.deleteTargetId();
    if (!id || this.isDeleting()) return;

    this.isDeleting.set(true);
    this.deleteErrorMessage.set(null);

    this.adminService.deleteRewardTier(id).subscribe({
      next: (res) => {
        if (res.success) {
          this.successMessage.set(res.message || 'Tier deleted successfully');
          this.showDeleteConfirm.set(false);
          this.deleteErrorMessage.set(null);
          this.deleteTargetId.set(null);
          this.deleteTargetName.set('');
          this.fetchTiers();
        } else {
          const message = 'Deletion failed: ' + (res.message || 'Unknown error');
          this.errorMessage.set(message);
          this.deleteErrorMessage.set(message);
        }
        this.isDeleting.set(false);
      },
      error: (err) => {
        const status = err?.status;
        const backendMessage = err?.error?.message || 'Failed to delete tier';
        if (status === 401 || status === 403) {
          this.errorMessage.set('Your session expired or you do not have permission to delete this tier. Please sign in again.');
          this.deleteErrorMessage.set(this.errorMessage());
          this.isDeleting.set(false);
          return;
        }

        if (status === 404) {
          this.errorMessage.set('Tier not found. It may have already been deleted. Refreshing list...');
          this.deleteErrorMessage.set(this.errorMessage());
          this.showDeleteConfirm.set(false);
          this.deleteTargetId.set(null);
          this.deleteTargetName.set('');
          this.fetchTiers();
          this.isDeleting.set(false);
          return;
        }

        const message = backendMessage.includes('existing reward accounts')
          ? `${backendMessage} This tier is currently in use by user reward accounts.`
          : backendMessage;

        this.errorMessage.set(message);
        this.deleteErrorMessage.set(message);
        this.isDeleting.set(false);
      }
    });
  }

  getTierStatus(tier: RewardTier): { label: string; color: string } {
    const now = new Date();
    const from = parseUtcDate(tier.effectiveFromUtc);
    const to = tier.effectiveToUtc ? parseUtcDate(tier.effectiveToUtc) : null;

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

  getNetworkLogoSrc(network: number): string | null {
    switch (network) {
      case 1: return '/assets/visa.png';
      case 2: return '/assets/mastercard.png';
      default: return null;
    }
  }

  hasUsableNetworkLogo(network: number): boolean {
    const src = this.getNetworkLogoSrc(network);
    return !!src && !this.brokenNetworkLogos()[src];
  }

  onNetworkLogoError(network: number): void {
    const src = this.getNetworkLogoSrc(network);
    if (!src) return;
    this.brokenNetworkLogos.update((current) => ({ ...current, [src]: true }));
  }

  getIssuerNameById(issuerId: string | null | undefined): string | null {
    if (!issuerId) return null;
    const issuer = this.issuers().find((item) => item.id === issuerId);
    return issuer?.name || null;
  }

  getTierBankName(tier: RewardTier): string {
    const issuerName = this.getIssuerNameById(tier.issuerId);
    if (issuerName) return issuerName;
    return `All Banks on ${this.getNetworkName(tier.cardNetwork)}`;
  }

  getTierScopeLabel(tier: RewardTier): string {
    return tier.issuerId ? 'Issuer Rule' : 'Network Rule';
  }

  getAvailableIssuersForSelectedNetwork(): CardIssuer[] {
    const selectedNetwork = this.tierForm.get('cardNetwork')?.value;
    return [...this.issuers()].sort((a, b) => {
      const aPriority = selectedNetwork && a.network === selectedNetwork ? 0 : 1;
      const bPriority = selectedNetwork && b.network === selectedNetwork ? 0 : 1;

      if (aPriority !== bPriority) {
        return aPriority - bPriority;
      }

      return a.name.localeCompare(b.name);
    });
  }

  onNetworkSelectionChange(): void {
    const selectedNetwork = this.tierForm.get('cardNetwork')?.value;
    const selectedIssuerId = this.tierForm.get('issuerId')?.value;
    if (!selectedNetwork || !selectedIssuerId) return;

    const selectedIssuer = this.issuers().find((issuer) => issuer.id === selectedIssuerId);
    if (!selectedIssuer || selectedIssuer.network !== selectedNetwork) {
      this.tierForm.patchValue({ issuerId: null });
    }
  }

  onIssuerSelectionChange(): void {
    const selectedIssuerId = this.tierForm.get('issuerId')?.value;
    if (!selectedIssuerId) return;

    const selectedIssuer = this.issuers().find((issuer) => issuer.id === selectedIssuerId);
    if (!selectedIssuer) return;

    const selectedNetwork = this.tierForm.get('cardNetwork')?.value;
    if (selectedNetwork !== selectedIssuer.network) {
      this.tierForm.patchValue({ cardNetwork: selectedIssuer.network });
    }
  }

  formatMinSpendForCard(minSpend: number): string {
    if (minSpend <= 0) return 'No minimum';
    const amount = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 }).format(minSpend);
    return `₹${amount}`;
  }

  formatRewardRate(rate: number): string {
    return (rate * 100).toFixed(1) + '%';
  }

  getCashbackDisplay(rate: number | null | undefined): string {
    if (!rate || rate === 0) return '0%';
    const pct = (rate * 100).toFixed(2).replace(/\.?0+$/, '');
    return `${pct}%`;
  }
}
