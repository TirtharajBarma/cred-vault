import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

type EligibleCard = {
  userId: string;
  userName: string;
  userEmail: string;
  cardId: string;
  issuerName: string;
  network: string;
  last4: string;
  outstandingBalance: number;
  creditLimit: number;
  currency: string;
};

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
  userSearchTerm = signal('');
  isUserDropdownOpen = signal(false);
  isLoadingEligibleCards = signal(false);
  eligibleCards = signal<EligibleCard[]>([]);

  filteredUsers = computed(() => {
    const query = this.userSearchTerm().trim().toLowerCase();
    const allUsers = this.users();

    if (!query) {
      return allUsers;
    }

    return allUsers.filter((user) => {
      const fullName = (user.fullName || '').toLowerCase();
      const email = (user.email || '').toLowerCase();
      return fullName.includes(query) || email.includes(query);
    });
  });

  billForm = this.fb.group({
    userId: ['', [Validators.required]],
    cardId: ['', [Validators.required]],
    currency: ['INR', [Validators.required]]
  });

  ngOnInit() {
    this.loadUsers();
    this.loadEligibleCardsForDemo();
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

  private loadCardsForUser(userId: string, preferredCardId?: string) {
    this.isLoadingCards.set(true);
    this.adminService.getCardsByUser(userId).subscribe({
      next: (res: any) => {
        const data = res.data?.data || res.data || [];
        this.cards.set(data);

        if (preferredCardId) {
          const preferredCard = data.find((card: any) => String(card.id) === String(preferredCardId));
          if (preferredCard) {
            this.billForm.get('cardId')?.setValue(String(preferredCard.id));
            this.selectedCardDetails.set(preferredCard);
          }
        }

        this.isLoadingCards.set(false);
      },
      error: (err) => {
        console.error('Failed to load cards:', err);
        this.isLoadingCards.set(false);
      }
    });
  }

  private resetSelectedUserState() {
    this.billForm.get('cardId')?.setValue('');
    this.billForm.get('userId')?.setValue('');
    this.cards.set([]);
    this.selectedUserName.set('');
    this.selectedCardDetails.set(null);
  }

  getUserLabel(user: any): string {
    return `${user.fullName} (${user.email})`;
  }

  onUserSearchInput(event: Event) {
    const value = (event.target as HTMLInputElement).value;
    this.userSearchTerm.set(value);
    this.isUserDropdownOpen.set(true);

    if (this.billForm.get('userId')?.value) {
      this.resetSelectedUserState();
    }
  }

  onUserInputFocus() {
    if (!this.isLoadingUsers()) {
      this.isUserDropdownOpen.set(true);
    }
  }

  onUserInputBlur() {
    setTimeout(() => {
      this.isUserDropdownOpen.set(false);
    }, 120);
  }

  selectUser(user: any) {
    this.applyUserSelection(user);
  }

  useEligibleCard(card: EligibleCard) {
    this.userSearchTerm.set(`${card.userName} (${card.userEmail})`);
    this.selectedUserName.set(card.userName);
    this.billForm.get('userId')?.setValue(card.userId);
    this.isUserDropdownOpen.set(false);
    this.loadCardsForUser(card.userId, card.cardId);
  }

  private applyUserSelection(user: any) {
    const userId = String(user.id);
    this.userSearchTerm.set(this.getUserLabel(user));
    this.selectedUserName.set(user.fullName || '');
    this.billForm.get('userId')?.setValue(userId);
    this.billForm.get('cardId')?.setValue('');
    this.cards.set([]);
    this.selectedCardDetails.set(null);
    this.isUserDropdownOpen.set(false);
    this.loadCardsForUser(userId);
  }

  clearUserSelection() {
    this.userSearchTerm.set('');
    this.isUserDropdownOpen.set(false);
    this.resetSelectedUserState();
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
          this.billForm.reset({ currency: 'INR' });
          this.cards.set([]);
          this.selectedUserName.set('');
          this.selectedCardDetails.set(null);
          this.userSearchTerm.set('');
          this.isUserDropdownOpen.set(false);
          this.loadEligibleCardsForDemo();
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

  isCheckingOverdue = signal(false);
  overdueResult = signal<string | null>(null);

  checkOverdue() {
    this.isCheckingOverdue.set(true);
    this.overdueResult.set(null);
    this.adminService.checkOverdue().subscribe({
      next: (res: any) => {
        this.overdueResult.set(res.message || 'Overdue check completed');
        this.isCheckingOverdue.set(false);
      },
      error: () => {
        this.overdueResult.set('Overdue check failed');
        this.isCheckingOverdue.set(false);
      }
    });
  }

  clearMessages() {
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }

  private loadEligibleCardsForDemo() {
    this.isLoadingEligibleCards.set(true);

    this.adminService.getAllUsersForDropdown().pipe(
      map((res: any) => {
        const data = res.data?.data || res.data || {};
        return (data.users || []).filter((user: any) => !!user?.id && !!user?.email);
      }),
      catchError(() => of([]))
    ).subscribe((users: any[]) => {
      if (!users.length) {
        this.eligibleCards.set([]);
        this.isLoadingEligibleCards.set(false);
        return;
      }

      const requests = users.map((user: any) =>
        this.adminService.getCardsByUser(user.id).pipe(
          map((res: any) => {
            const cards = res.data?.data || res.data || [];
            return { user, cards: Array.isArray(cards) ? cards : [] };
          }),
          catchError(() => of({ user, cards: [] }))
        )
      );

      forkJoin(requests).subscribe({
        next: (results: Array<{ user: any; cards: any[] }>) => {
          const cards = results.flatMap(({ user, cards }) =>
            cards
              .filter((card: any) => Number(card?.outstandingBalance ?? 0) > 0)
              .map((card: any) => ({
                userId: String(user.id),
                userName: user.fullName || 'User',
                userEmail: user.email || '',
                cardId: String(card.id),
                issuerName: card.issuerName || 'Card',
                network: this.getNetworkName(card.network),
                last4: card.last4 || '----',
                outstandingBalance: Number(card.outstandingBalance || 0),
                creditLimit: Number(card.creditLimit || 0),
                currency: card.currency || 'INR'
              }))
          );

          cards.sort((a, b) => b.outstandingBalance - a.outstandingBalance);
          this.eligibleCards.set(cards);
          this.isLoadingEligibleCards.set(false);
        },
        error: () => {
          this.eligibleCards.set([]);
          this.isLoadingEligibleCards.set(false);
        }
      });
    });
  }

  getNetworkName(network: unknown): string {
    const normalized = String(network ?? '').trim().toLowerCase();

    if (normalized === '1' || normalized === 'visa') {
      return 'Visa';
    }

    if (normalized === '2' || normalized === 'mastercard') {
      return 'Mastercard';
    }

    return normalized ? String(network) : 'Unknown';
  }

  getAvailableBalance(): number {
    const card = this.selectedCardDetails();
    if (!card) return 0;
    return (card.creditLimit || 0) - (card.outstandingBalance || 0);
  }

  getSelectedCurrencyCode(): string {
    return this.selectedCardDetails()?.currency || this.billForm.get('currency')?.value || 'INR';
  }

  formatAmount(amount: number | null | undefined, currencyCode?: string): string {
    const safeAmount = Number(amount || 0);
    const safeCurrency = currencyCode || this.getSelectedCurrencyCode();

    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: safeCurrency,
      maximumFractionDigits: 0
    }).format(safeAmount);
  }
}
