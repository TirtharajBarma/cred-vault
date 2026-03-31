import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-reward-tiers',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './reward-tiers.component.html'
})
export class RewardTiersComponent implements OnInit {
  private adminService = inject(AdminService);
  private fb = inject(FormBuilder);
  
  tiers = signal<any[]>([]);
  isLoading = signal(true);
  isSubmitting = signal(false);
  showModal = signal(false);
  
  // Delete confirmation modal
  showDeleteConfirm = signal(false);
  deleteTargetId = signal<string | null>(null);

  tierForm = this.fb.group({
    cardNetwork: [1, [Validators.required]],
    issuerId: [null],
    minSpend: [0, [Validators.required, Validators.min(0)]],
    rewardRate: [0.01, [Validators.required, Validators.min(0), Validators.max(1)]],
    effectiveFromUtc: [new Date().toISOString().split('T')[0], [Validators.required]],
    effectiveToUtc: [null]
  });

  ngOnInit() {
    this.fetchTiers();
  }

  fetchTiers() {
    this.isLoading.set(true);
    this.adminService.getRewardTiers().subscribe({
      next: (res: any) => {
        this.tiers.set(res.data?.data || res.data || []);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
      }
    });
  }

  onSubmit() {
    if (this.tierForm.invalid) return;
    
    this.isSubmitting.set(true);
    const data = { ...this.tierForm.value };
    if (!data.issuerId) delete data.issuerId;
    
    this.adminService.createRewardTier(data).subscribe({
      next: (res) => {
        if (res.success) {
          this.fetchTiers();
          this.closeModal();
        }
        this.isSubmitting.set(false);
      },
      error: () => this.isSubmitting.set(false)
    });
  }

  openModal() {
    this.tierForm.reset({ cardNetwork: 1, minSpend: 0, rewardRate: 0.01, effectiveFromUtc: new Date().toISOString().split('T')[0] });
    this.showModal.set(true);
  }

  closeModal() {
    this.showModal.set(false);
  }

  confirmDelete(id: string) {
    this.deleteTargetId.set(id);
    this.showDeleteConfirm.set(true);
  }

  cancelDelete() {
    this.showDeleteConfirm.set(false);
    this.deleteTargetId.set(null);
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
          alert('Deletion failed: ' + (res.message || 'Unknown error'));
        }
        this.deleteTargetId.set(null);
      },
      error: () => {
        alert('Critical error during deletion.');
        this.deleteTargetId.set(null);
      }
    });
  }
}
