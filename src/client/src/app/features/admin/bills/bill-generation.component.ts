import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-bill-generation',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './bill-generation.component.html'
})
export class BillGenerationComponent implements OnInit {
  private adminService = inject(AdminService);
  private fb = inject(FormBuilder);
  
  isSubmitting = signal(false);
  successMessage = signal<string | null>(null);
  errorMessage = signal<string | null>(null);
  
  users = signal<any[]>([]);
  cards = signal<any[]>([]);
  isLoadingCards = signal(false);

  billForm = this.fb.group({
    userId: ['', [Validators.required]],
    cardId: ['', [Validators.required]],
    currency: ['USD', [Validators.required]]
  });

  ngOnInit() {
    this.loadUsers();
  }

  loadUsers() {
    this.adminService.getAllUsersForDropdown().subscribe({
      next: (res: any) => {
        const userList = res.data?.data || res.data || [];
        this.users.set(userList.filter((u: any) => u.status === 'active'));
      },
      error: () => {}
    });
  }

  onUserChange(event: Event) {
    const userId = (event.target as HTMLSelectElement).value;
    this.billForm.get('cardId')?.setValue('');
    this.cards.set([]);
    
    if (userId) {
      this.isLoadingCards.set(true);
      this.adminService.getCardsByUser(userId).subscribe({
        next: (res: any) => {
          this.cards.set(res.data?.data || res.data || []);
          this.isLoadingCards.set(false);
        },
        error: () => this.isLoadingCards.set(false)
      });
    }
  }

  generateBill() {
    if (this.billForm.invalid) return;
    
    this.isSubmitting.set(true);
    this.successMessage.set(null);
    this.errorMessage.set(null);
    
    this.adminService.generateBill(this.billForm.value).subscribe({
      next: (res) => {
        if (res.success) {
          this.successMessage.set('Billing cycle executed successfully. User notified.');
          this.billForm.reset({ currency: 'USD' });
        } else {
          this.errorMessage.set(res.message || 'Billing protocol failed.');
        }
        this.isSubmitting.set(false);
      },
      error: (err) => {
        this.errorMessage.set('Strategic failure in billing engine.');
        this.isSubmitting.set(false);
      }
    });
  }
}
