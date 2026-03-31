import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-issuer-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './issuer-management.component.html'
})
export class IssuerManagementComponent implements OnInit {
  private adminService = inject(AdminService);
  private fb = inject(FormBuilder);
  
  issuers = signal<any[]>([]);
  isLoading = signal(true);
  isSubmitting = signal(false);
  showModal = signal(false);
  
  // Delete confirmation modal
  showDeleteConfirm = signal(false);
  deleteTargetId = signal<string | null>(null);

  issuerForm = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(3)]],
    network: [1, [Validators.required]],
    isActive: [true]
  });

  ngOnInit() {
    this.fetchIssuers();
  }

  fetchIssuers() {
    this.isLoading.set(true);
    this.adminService.getIssuers().subscribe({
      next: (res: any) => {
        this.issuers.set(res.data?.data || res.data || []);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
      }
    });
  }

  onSubmit() {
    if (this.issuerForm.invalid) return;
    
    this.isSubmitting.set(true);
    const formValue = this.issuerForm.value;
    
    const networkMap: { [key: number]: string } = {
      1: 'Visa',
      2: 'Mastercard'
    };
    
    const payload = {
      name: formValue.name,
      network: networkMap[Number(formValue.network)] || 'Visa',
      isActive: formValue.isActive
    };

    this.adminService.createIssuer(payload).subscribe({
      next: (res) => {
        if (res.success) {
          alert('Fintech partner authorized successfully.');
          this.fetchIssuers();
          this.closeModal();
        } else {
          alert('Authorization failed: ' + (res.message || 'Unknown server error'));
        }
        this.isSubmitting.set(false);
      },
      error: (err) => {
        alert('Critical error during partner onboarding. Please check system logs.');
        this.isSubmitting.set(false);
      }
    });
  }

  openModal() {
    this.issuerForm.reset({ network: 1, isActive: true });
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

  deleteIssuer() {
    const id = this.deleteTargetId();
    if (!id) return;
    
    this.showDeleteConfirm.set(false);
    this.adminService.deleteIssuer(id).subscribe({
      next: (res) => {
        if (res.success) {
          this.fetchIssuers();
        } else {
          alert('Deletion failed: ' + (res.message || 'Unknown error'));
        }
        this.deleteTargetId.set(null);
      },
      error: (err) => {
        alert('Critical error during deletion.');
        this.deleteTargetId.set(null);
      }
    });
  }
}