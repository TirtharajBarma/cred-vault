import { Component, OnDestroy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-issuer-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './issuer-management.component.html'
})
export class IssuerManagementComponent implements OnInit, OnDestroy {
  private adminService = inject(AdminService);
  private fb = inject(FormBuilder);
  
  issuers = signal<any[]>([]);
  searchQuery = signal('');
  isLoading = signal(true);
  isSubmitting = signal(false);
  showModal = signal(false);
  isEditMode = signal(false);
  editingIssuer = signal<any>(null);
  
  showDeleteConfirm = signal(false);
  deleteTargetId = signal<string | null>(null);
  deleteTargetName = signal<string>('');

  successMessage = signal<string | null>(null);
  errorMessage = signal<string | null>(null);
  toastMessage = signal<string | null>(null);
  private messageTimer: ReturnType<typeof setTimeout> | null = null;
  private toastTimer: ReturnType<typeof setTimeout> | null = null;

  filteredIssuers = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const source = this.issuers();
    if (!query) return source;

    return source.filter((issuer) => {
      const name = String(issuer?.name ?? '').toLowerCase();
      const network = String(issuer?.network ?? '').toLowerCase();
      return name.includes(query) || network.includes(query);
    });
  });

  issuerForm = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    network: [1, [Validators.required]]
  });

  ngOnInit() {
    this.fetchIssuers();
  }

  ngOnDestroy() {
    if (this.messageTimer) clearTimeout(this.messageTimer);
    if (this.toastTimer) clearTimeout(this.toastTimer);
  }

  fetchIssuers() {
    this.isLoading.set(true);
    this.adminService.getIssuers().subscribe({
      next: (res: any) => {
        this.issuers.set(res.data?.data || res.data || []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.showError('Failed to load issuers');
      }
    });
  }

  openModal(issuer?: any) {
    this.successMessage.set(null);
    this.errorMessage.set(null);
    
    if (issuer) {
      this.isEditMode.set(true);
      this.editingIssuer.set(issuer);
      this.issuerForm.patchValue({
        name: issuer.name,
        network: issuer.network === 'Visa' || issuer.network === 1 ? 1 : 2
      });
    } else {
      this.isEditMode.set(false);
      this.editingIssuer.set(null);
      this.issuerForm.reset({ network: 1 });
    }
    this.showModal.set(true);
  }

  closeModal() {
    this.showModal.set(false);
    this.isEditMode.set(false);
    this.editingIssuer.set(null);
    this.issuerForm.reset({ network: 1 });
  }

  onSubmit() {
    if (this.issuerForm.invalid) return;
    
    this.isSubmitting.set(true);
    this.successMessage.set(null);
    this.errorMessage.set(null);
    
    const formValue = this.issuerForm.value;
    const networkMap: { [key: number]: string } = {
      1: 'Visa',
      2: 'Mastercard'
    };
    
    const payload = {
      name: formValue.name!,
      network: networkMap[Number(formValue.network)] || 'Visa'
    };

    if (this.isEditMode()) {
      const id = this.editingIssuer()?.id;
      if (!id) return;
      
      this.adminService.updateIssuer(id, payload).subscribe({
        next: (res: any) => {
          if (res.success) {
            this.showSuccess('Issuer updated');
            this.showToast('Issuer updated');
            this.fetchIssuers();
            setTimeout(() => this.closeModal(), 1000);
          } else {
            this.showError(res.message || 'Update failed');
          }
          this.isSubmitting.set(false);
        },
        error: () => {
          this.showError('Failed to update issuer');
          this.isSubmitting.set(false);
        }
      });
    } else {
      this.adminService.createIssuer(payload).subscribe({
        next: (res: any) => {
          if (res.success) {
            this.showSuccess('Issuer added');
            this.showToast('Issuer added');
            this.fetchIssuers();
            setTimeout(() => this.closeModal(), 1000);
          } else {
            this.showError(res.message || 'Failed to add issuer');
          }
          this.isSubmitting.set(false);
        },
        error: () => {
          this.showError('Failed to add issuer');
          this.isSubmitting.set(false);
        }
      });
    }
  }

  confirmDelete(issuer: any) {
    this.deleteTargetId.set(issuer.id);
    this.deleteTargetName.set(issuer.name);
    this.showDeleteConfirm.set(true);
  }

  cancelDelete() {
    this.showDeleteConfirm.set(false);
    this.deleteTargetId.set(null);
    this.deleteTargetName.set('');
  }

  deleteIssuer() {
    const id = this.deleteTargetId();
    if (!id) return;
    
    this.showDeleteConfirm.set(false);
    this.adminService.deleteIssuer(id).subscribe({
      next: (res: any) => {
        if (res.success) {
          this.showSuccess('Issuer deleted successfully');
          this.showToast('Issuer deleted');
          this.fetchIssuers();
        } else {
          this.showError(res.message || 'Delete failed');
        }
        this.deleteTargetId.set(null);
        this.deleteTargetName.set('');
      },
      error: (err: any) => {
        const message = err?.error?.message || 'Delete failed';
        this.showError(message);
        this.deleteTargetId.set(null);
        this.deleteTargetName.set('');
      }
    });
  }

  private showSuccess(message: string) {
    this.successMessage.set(message);
    this.errorMessage.set(null);
    if (this.messageTimer) clearTimeout(this.messageTimer);
    this.messageTimer = setTimeout(() => this.successMessage.set(null), 2500);
  }

  private showError(message: string) {
    this.errorMessage.set(message);
    this.successMessage.set(null);
    if (this.messageTimer) clearTimeout(this.messageTimer);
    this.messageTimer = setTimeout(() => this.errorMessage.set(null), 3000);
  }

  private showToast(message: string) {
    this.toastMessage.set(message);
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toastMessage.set(null), 2000);
  }
}
