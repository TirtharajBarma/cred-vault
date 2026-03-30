import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { CreditCard, CardTransaction, TransactionType } from '../../core/models/card.models';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { delay, catchError } from 'rxjs/operators';

import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  cards = signal<CreditCard[]>([]);
  transactions = signal<CardTransaction[]>([]);
  issuers = signal<any[]>([]);
  isLoading = signal(true);
  
  user = this.authService.currentUser;
  
  showAddCardModal = signal(false);
  addCardForm: FormGroup;
  isSubmitting = signal(false);
  errorMessage = signal<string | null>(null);

  constructor() {
    this.addCardForm = this.fb.group({
      cardholderName: ['', [Validators.required]],
      cardNumber: ['', [Validators.required, Validators.pattern(/^\d{16}$/)]],
      expMonth: [1, [Validators.required, Validators.min(1), Validators.max(12)]],
      expYear: [new Date().getFullYear(), [Validators.required]],
      issuerId: ['', [Validators.required]],
      isDefault: [false]
    });
  }

  ngOnInit(): void {
    this.loadDashboardData();
    this.loadIssuers();
  }

  loadDashboardData(): void {
    this.isLoading.set(true);
    
    const cards$ = this.dashboardService.getCards().pipe(
      catchError(() => of({ success: false, data: [] }))
    );
    
    const transactions$ = this.dashboardService.getAllTransactions().pipe(
      catchError(() => of({ success: false, data: [] }))
    );

    forkJoin({
      cards: cards$,
      transactions: transactions$
    }).pipe(
      delay(800)
    ).subscribe({
      next: (res: any) => {
        if (res.cards.success) {
          this.cards.set(res.cards.data || []);
        }
        if (res.transactions.success) {
          this.transactions.set(res.transactions.data || []);
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('[Dashboard] Unexpected error:', err);
        this.isLoading.set(false);
      }
    });
  }

  loadIssuers(): void {
    this.dashboardService.getIssuers().subscribe(res => {
      if (res.success) this.issuers.set(res.data || []);
    });
  }

  toggleAddCardModal(): void {
    this.showAddCardModal.set(!this.showAddCardModal());
    this.errorMessage.set(null);
    if (!this.showAddCardModal()) {
      this.addCardForm.reset({ expMonth: 1, expYear: new Date().getFullYear(), isDefault: false });
    }
  }

  onSubmitCard(): void {
    if (this.addCardForm.invalid) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    
    this.dashboardService.addCard(this.addCardForm.value).subscribe({
      next: (res) => {
        if (res.success) {
          this.loadDashboardData();
          this.toggleAddCardModal();
        } else {
          this.errorMessage.set(res.message || 'Failed to add card.');
        }
        this.isSubmitting.set(false);
      },
      error: (err) => {
        const msg = err?.error?.message || 'A server error occurred. Please try again.';
        this.errorMessage.set(msg);
        this.isSubmitting.set(false);
      }
    });
  }

  getTransactionIcon(type: TransactionType): string {
    switch (type) {
      case TransactionType.Purchase: return 'shopping_cart';
      case TransactionType.Payment: return 'payments';
      case TransactionType.Refund: return 'settings_backup_restore';
      default: return 'receipt';
    }
  }

  getCardGradient(index: number): string {
    const gradients = ['card-gradient-1', 'card-gradient-2', 'card-gradient-3'];
    return gradients[index % gradients.length];
  }

  getTotalAssets(): number {
    return this.cards().reduce((acc, card) => acc + card.creditLimit, 0);
  }
}
