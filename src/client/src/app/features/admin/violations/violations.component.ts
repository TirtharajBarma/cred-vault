import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../../core/services/admin.service';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

@Component({
  selector: 'app-violations',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './violations.component.html'
})
export class ViolationsComponent implements OnInit {
  private adminService = inject(AdminService);

  blockedCards = signal<any[]>([]);
  violations = signal<any[]>([]);
  isLoading = signal(true);
  isLoadingViolations = signal(false);
  showViolationsModal = signal(false);
  selectedCardId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadBlockedCards();
  }

  loadBlockedCards(): void {
    this.isLoading.set(true);
    this.adminService.getBlockedCards().pipe(
      catchError(() => of({ success: false, data: [] }))
    ).subscribe(res => {
      if (res.success) {
        this.blockedCards.set(res.data || []);
      }
      this.isLoading.set(false);
    });
  }

  unblockCard(card: any): void {
    if (confirm(`Unblock card ending in ${card.last4}?`)) {
      this.adminService.unblockCard(card.id).subscribe({
        next: (res) => {
          if (res.success) {
            this.loadBlockedCards();
          } else {
            alert(res.message || 'Failed to unblock card');
          }
        },
        error: () => alert('Error unblocking card')
      });
    }
  }

  viewViolations(cardId: string): void {
    this.selectedCardId.set(cardId);
    this.showViolationsModal.set(true);
    this.isLoadingViolations.set(true);

    this.adminService.getCardViolations(cardId).pipe(
      catchError(() => of({ success: false, data: [] }))
    ).subscribe(res => {
      if (res.success) {
        this.violations.set(res.data || []);
      }
      this.isLoadingViolations.set(false);
    });
  }

  clearViolations(cardId: string): void {
    if (confirm('Clear all violations for this card?')) {
      this.adminService.clearCardViolations(cardId).subscribe({
        next: (res) => {
          if (res.success) {
            this.showViolationsModal.set(false);
            this.loadBlockedCards();
          } else {
            alert(res.message || 'Failed to clear violations');
          }
        },
        error: () => alert('Error clearing violations')
      });
    }
  }
}
