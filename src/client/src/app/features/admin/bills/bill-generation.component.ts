import { Component, inject, signal, OnInit, computed } from '@angular/core';
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
  isLoadingUsers = signal(true);
  
  users = signal<any[]>([]);
  cards = signal<any[]>([]);
  isLoadingCards = signal(false);
  selectedUserName = signal<string>('');
  selectedCardDetails = signal<any>(null);

  billForm = this.fb.group({
    userId: ['', [Validators.required]],
    cardId: ['', [Validators.required]],
    currency: ['USD', [Validators.required]]
  });

  ngOnInit() {
    this.loadUsers();
  }

  loadUsers() {
    this.isLoadingUsers.set(true);
    this.adminService.getAllUsersForDropdown().subscribe({
      next: (res: any) => {
        const data = res.data?.data || res.data || {};
        const allUsers = data.users || [];
        // Filter to show users that might have cards (active users with email)
        this.users.set(allUsers.filter((u: any) => u.email));
        this.isLoadingUsers.set(false);
      },
      error: (err) => {
        console.error('Failed to load users:', err);
        this.isLoadingUsers.set(false);
        this.errorMessage.set('Failed to load users');
      }
    });
  }

  onUserChange(event: Event) {
    const userId = (event.target as HTMLSelectElement).value;
    this.billForm.get('cardId')?.setValue('');
    this.cards.set([]);
    this.selectedUserName.set('');
    this.selectedCardDetails.set(null);
    
    if (userId) {
      const user = this.users().find(u => u.id === userId);
      this.selectedUserName.set(user?.fullName || '');
      
      this.isLoadingCards.set(true);
      this.adminService.getCardsByUser(userId).subscribe({
        next: (res: any) => {
          const data = res.data?.data || res.data || [];
          this.cards.set(data);
          this.isLoadingCards.set(false);
        },
        error: (err) => {
          console.error('Failed to load cards:', err);
          this.isLoadingCards.set(false);
        }
      });
    }
  }

  onCardChange(event: Event) {
    const cardId = (event.target as HTMLSelectElement).value;
    if (cardId) {
      const card = this.cards().find(c => c.id === cardId);
      this.selectedCardDetails.set(card || null);
    } else {
      this.selectedCardDetails.set(null);
    }
  }

  generateBill() {
    if (this.billForm.invalid) return;
    
    this.isSubmitting.set(true);
    this.successMessage.set(null);
    this.errorMessage.set(null);
    
    const formValue = this.billForm.value;
    
    this.adminService.generateBill({
      userId: formValue.userId!,
      cardId: formValue.cardId!,
      currency: formValue.currency!
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.successMessage.set(`Billing cycle executed successfully for ${this.selectedUserName()}. User will be notified.`);
          this.billForm.reset({ currency: 'USD' });
          this.cards.set([]);
          this.selectedUserName.set('');
          this.selectedCardDetails.set(null);
        } else {
          this.errorMessage.set(res.message || 'Billing protocol failed');
        }
        this.isSubmitting.set(false);
      },
      error: (err) => {
        console.error('Bill generation error:', err);
        const msg = err?.error?.message || err?.error?.Message || err?.message || 'Failed to generate bill. Check card balance or try again.';
        this.errorMessage.set(msg);
        this.isSubmitting.set(false);
      }
    });
  }

  clearMessages() {
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }

  getAvailableBalance(): number {
    const card = this.selectedCardDetails();
    if (!card) return 0;
    return (card.creditLimit || 0) - (card.outstandingBalance || 0);
  }
}
