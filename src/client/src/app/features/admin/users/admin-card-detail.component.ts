import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-admin-card-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <!-- Back Button -->
      <button (click)="goBack()" class="flex items-center gap-2 text-gray-600 hover:text-gray-900 mb-4">
        <span>←</span> Back to User
      </button>

      @if (isLoading()) {
        <div class="flex justify-center py-20">
          <div class="animate-spin w-10 h-10 border-4 border-gray-300 border-t-gray-800 rounded-full"></div>
        </div>
      } @else if (card()) {
        <!-- Card Header -->
        <div class="bg-gradient-to-r from-slate-800 to-slate-900 rounded-2xl p-6 text-white mb-6">
          <div class="flex justify-between items-start">
            <div>
              <div class="flex items-center gap-3 mb-2">
                <span class="text-3xl">{{ card()?.network === 'Visa' ? '💳' : '💠' }}</span>
                <div>
                  <h1 class="text-2xl font-bold">{{ card()?.issuerName || 'Credit Card' }}</h1>
                  <p class="text-gray-300">{{ card()?.network }} •••• {{ card()?.last4 }}</p>
                </div>
              </div>
            </div>
            <div class="text-right">
              @if (card()?.isBlocked) {
                <span class="px-4 py-2 bg-red-500 text-white rounded-lg text-lg font-bold">BLOCKED</span>
              } @else if ((card()?.creditLimit ?? 0) <= 0) {
                <span class="px-4 py-2 bg-amber-500 text-white rounded-lg text-lg font-bold">NEEDS SETUP</span>
              } @else {
                <span class="px-4 py-2 bg-emerald-500 text-white rounded-lg text-lg font-bold">ACTIVE</span>
              }
            </div>
          </div>
        </div>

        <!-- Card Details Grid -->
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <!-- Left: Card Info -->
          <div class="lg:col-span-2 space-y-6">
            <!-- Balance Overview -->
            <div class="bg-white rounded-xl border border-gray-200 p-6">
              <h2 class="font-bold text-gray-900 mb-4">Balance Overview</h2>
              <div class="grid grid-cols-3 gap-6">
                <div class="text-center p-4 bg-gray-50 rounded-xl">
                  <p class="text-sm text-gray-500 mb-1">Credit Limit</p>
                  <p class="text-2xl font-bold text-gray-900">{{ card()?.creditLimit | currency }}</p>
                </div>
                <div class="text-center p-4 bg-red-50 rounded-xl">
                  <p class="text-sm text-gray-500 mb-1">Outstanding</p>
                  <p class="text-2xl font-bold text-red-600">{{ card()?.outstandingBalance | currency }}</p>
                </div>
                <div class="text-center p-4 bg-emerald-50 rounded-xl">
                  <p class="text-sm text-gray-500 mb-1">Available</p>
                  <p class="text-2xl font-bold text-emerald-600">{{ (card()?.creditLimit - card()?.outstandingBalance) | currency }}</p>
                </div>
              </div>

              @if ((card()?.creditLimit ?? 0) > 0) {
                <div class="mt-4">
                  <div class="flex justify-between text-sm mb-1">
                    <span>Utilization</span>
                    <span>{{ utilizationPercent() | number:'1.0-0' }}%</span>
                  </div>
                  <div class="h-3 bg-gray-200 rounded-full overflow-hidden">
                    <div class="h-full bg-gradient-to-r from-emerald-500 to-red-500" 
                         [style.width.%]="utilizationPercent()"></div>
                  </div>
                </div>
              }
            </div>

            <!-- Violations -->
            <div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
              <div class="px-6 py-4 border-b border-gray-200 bg-gray-50 flex justify-between items-center">
                <h2 class="font-bold text-gray-900">Violations ({{ violations().length }})</h2>
                @if (violations().length > 0) {
                  <button (click)="clearViolations()" class="px-4 py-1.5 bg-red-100 text-red-700 rounded-lg text-sm font-medium hover:bg-red-200">
                    Clear All
                  </button>
                }
              </div>
              @if (isLoadingViolations()) {
                <div class="p-8 text-center">
                  <div class="animate-spin w-8 h-8 border-4 border-gray-300 border-t-gray-800 rounded-full mx-auto"></div>
                </div>
              } @else if (violations().length > 0) {
                <div class="divide-y divide-gray-100">
                  @for (v of violations(); track v.id) {
                    <div class="px-6 py-4">
                      <div class="flex justify-between items-start">
                        <div>
                          <p class="font-semibold text-gray-900 flex items-center gap-2">
                            <span class="text-red-500">⚠️</span> {{ v.reason }}
                          </p>
                          <p class="text-sm text-gray-500 mt-1">Strike #{{ v.strikeCount }} • Penalty: {{ v.penaltyAmount | currency }}</p>
                        </div>
                        <span class="text-xs text-gray-400">{{ formatDate(v.createdAtUtc) }}</span>
                      </div>
                    </div>
                  }
                </div>
              } @else {
                <div class="p-8 text-center text-emerald-600">
                  <p class="text-2xl mb-2">✓</p>
                  <p class="font-semibold">No violations on this card</p>
                </div>
              }
            </div>

            <!-- Statements -->
            <div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
              <div class="px-6 py-4 border-b border-gray-200 bg-gray-50">
                <h2 class="font-bold text-gray-900">Statements ({{ statements().length }})</h2>
              </div>
              @if (isLoadingStatements()) {
                <div class="p-8 text-center">
                  <div class="animate-spin w-8 h-8 border-4 border-gray-300 border-t-gray-800 rounded-full mx-auto"></div>
                </div>
              } @else if (statements().length > 0) {
                <div class="max-h-96 overflow-y-auto divide-y divide-gray-100">
                  @for (stmt of statements(); track stmt.id) {
                    <div (click)="viewStatement(stmt)" 
                         class="px-6 py-4 hover:bg-blue-50 cursor-pointer transition-all flex justify-between items-center">
                      <div>
                        <p class="font-semibold text-gray-900">{{ stmt.statementPeriod || 'Statement' }}</p>
                        <p class="text-sm text-gray-500">{{ stmt.cardLast4 }} • {{ stmt.cardNetwork }}</p>
                      </div>
                      <div class="text-right">
                        <p class="font-bold text-lg">{{ stmt.closingBalance || stmt.totalAmount | currency }}</p>
                        <p class="text-sm text-gray-400">→ View Details</p>
                      </div>
                    </div>
                  }
                </div>
              } @else {
                <div class="p-8 text-center text-gray-500">
                  <p class="text-2xl mb-2">📊</p>
                  <p>No statements generated</p>
                </div>
              }
            </div>
          </div>

          <!-- Right: Admin Controls -->
          <div class="space-y-6">
            <!-- Card Details -->
            <div class="bg-white rounded-xl border border-gray-200 p-6">
              <h3 class="font-bold text-gray-900 mb-4">Card Details</h3>
              <div class="space-y-3 text-sm">
                <div class="flex justify-between">
                  <span class="text-gray-500">Card Number</span>
                  <span class="font-mono">•••• {{ card()?.last4 }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-gray-500">Network</span>
                  <span>{{ card()?.network }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-gray-500">Expires</span>
                  <span>{{ card()?.expMonth }}/{{ card()?.expYear }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-gray-500">Billing Day</span>
                  <span>Day {{ card()?.billingCycleStartDay }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-gray-500">Status</span>
                  <span>{{ card()?.isBlocked ? 'Blocked' : 'Active' }}</span>
                </div>
              </div>
            </div>

            <!-- Admin Controls -->
            <div class="bg-white rounded-xl border border-gray-200 p-6">
              <h3 class="font-bold text-gray-900 mb-4">Admin Controls</h3>
              
              @if ((card()?.creditLimit ?? 0) <= 0) {
                <div class="p-3 bg-amber-50 border border-amber-200 rounded-lg text-amber-700 text-sm mb-4">
                  Set credit limit before billing can work
                </div>
              }

              <div class="space-y-4">
                <div>
                  <label class="text-xs text-gray-500 uppercase mb-1 block">Credit Limit</label>
                  <input [(ngModel)]="editCreditLimit" type="number" min="1" step="100"
                         class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm" />
                </div>
                <div>
                  <label class="text-xs text-gray-500 uppercase mb-1 block">Outstanding Balance</label>
                  <input [(ngModel)]="editOutstandingBalance" type="number" min="0" step="10"
                         class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm" />
                </div>
                <div>
                  <label class="text-xs text-gray-500 uppercase mb-1 block">Billing Cycle Start Day</label>
                  <input [(ngModel)]="editBillingDay" type="number" min="1" max="31"
                         class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm" />
                </div>

                @if (updateMessage()) {
                  <div class="p-2 rounded-lg text-sm" 
                       [class]="updateSuccess() ? 'bg-emerald-50 text-emerald-700' : 'bg-red-50 text-red-700'">
                    {{ updateMessage() }}
                  </div>
                }

                <button (click)="saveChanges()" [disabled]="isSaving()"
                        class="w-full px-4 py-2.5 bg-gray-800 text-white rounded-lg font-medium hover:bg-gray-700 disabled:opacity-50">
                  {{ isSaving() ? 'Saving...' : 'Save Changes' }}
                </button>

                @if (card()?.isBlocked) {
                  <button (click)="unblockCard()"
                          class="w-full px-4 py-2.5 bg-emerald-500 text-white rounded-lg font-medium hover:bg-emerald-600">
                    Unblock Card
                  </button>
                }
              </div>
            </div>
          </div>
        </div>
      } @else {
        <div class="text-center py-20">
          <p class="text-gray-500">Card not found</p>
          <button (click)="goBack()" class="mt-4 px-4 py-2 bg-gray-800 text-white rounded-lg">Go Back</button>
        </div>
      }
    </div>
  `
})
export class AdminCardDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private adminService = inject(AdminService);

  card = signal<any>(null);
  violations = signal<any[]>([]);
  statements = signal<any[]>([]);

  isLoading = signal(true);
  isLoadingViolations = signal(false);
  isLoadingStatements = signal(false);
  isSaving = signal(false);
  updateMessage = signal<string | null>(null);
  updateSuccess = signal(false);

  editCreditLimit: number | null = null;
  editOutstandingBalance: number | null = null;
  editBillingDay: number | null = null;

  userId: string | null = null;
  cardId: string | null = null;

  ngOnInit() {
    this.userId = this.route.snapshot.paramMap.get('userId');
    this.cardId = this.route.snapshot.paramMap.get('cardId');
    if (this.cardId) {
      this.loadCard(this.cardId);
      this.loadViolations(this.cardId);
      this.loadStatements(this.cardId);
    }
  }

  loadCard(cardId: string) {
    this.adminService.getCardsByUser(this.userId!).subscribe({
      next: (res) => {
        const cards = res.data?.data || res.data || [];
        const card = cards.find((c: any) => c.id === cardId);
        if (card) {
          this.card.set(card);
          this.editCreditLimit = card.creditLimit;
          this.editOutstandingBalance = card.outstandingBalance;
          this.editBillingDay = card.billingCycleStartDay;
        }
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  loadViolations(cardId: string) {
    this.isLoadingViolations.set(true);
    this.adminService.getCardViolations(cardId).subscribe({
      next: (res) => {
        const rows = res.data?.data || res.data || [];
        this.violations.set((Array.isArray(rows) ? rows : []).filter((v: any) => v?.isActive));
        this.isLoadingViolations.set(false);
      },
      error: () => this.isLoadingViolations.set(false)
    });
  }

  loadStatements(cardId: string) {
    if (!this.userId) {
      this.statements.set([]);
      return;
    }

    this.isLoadingStatements.set(true);
    this.adminService.getAllStatementsForAdmin().subscribe({
      next: (res) => {
        const data = res.data?.data || res.data || res;
        const statements = Array.isArray(data) ? data : (data.statements || []);
        this.statements.set(
          statements.filter((stmt: any) => {
            const stmtCardId = String(stmt?.cardId || stmt?.CardId || '');
            return stmtCardId === cardId;
          })
        );
        this.isLoadingStatements.set(false);
      },
      error: () => {
        this.statements.set([]);
        this.isLoadingStatements.set(false);
      }
    });
  }

  goBack() {
    if (this.userId) {
      this.router.navigate(['/admin/users', this.userId]);
    } else {
      this.router.navigate(['/admin/users']);
    }
  }

  viewStatement(stmt: any) {
    this.router.navigate(['/admin/users', this.userId, 'cards', this.cardId, 'statements', stmt.id]);
  }

  utilizationPercent(): number {
    const card = this.card();
    if (!card || card.creditLimit <= 0) return 0;
    return (card.outstandingBalance / card.creditLimit) * 100;
  }

  saveChanges() {
    const card = this.card();
    if (!card) return;

    const creditLimit = Number(this.editCreditLimit);
    if (!Number.isFinite(creditLimit) || creditLimit <= 0) {
      this.updateMessage.set('Credit limit must be greater than 0');
      this.updateSuccess.set(false);
      return;
    }

    this.isSaving.set(true);
    this.adminService.updateCardByAdmin(card.id, {
      creditLimit,
      outstandingBalance: this.editOutstandingBalance,
      billingCycleStartDay: this.editBillingDay
    }).subscribe({
      next: (res) => {
        this.isSaving.set(false);
        if (res.success) {
          this.card.set({ ...card, ...res.data });
          this.updateMessage.set('Card updated successfully');
          this.updateSuccess.set(true);
        } else {
          this.updateMessage.set(res.message || 'Failed to update');
          this.updateSuccess.set(false);
        }
      },
      error: () => {
        this.isSaving.set(false);
        this.updateMessage.set('Failed to update card');
        this.updateSuccess.set(false);
      }
    });
  }

  unblockCard() {
    const card = this.card();
    if (!card) return;
    this.adminService.unblockCard(card.id).subscribe({
      next: (res) => {
        if (res.success) {
          this.card.set({ ...card, isBlocked: false });
        }
      }
    });
  }

  clearViolations() {
    const card = this.card();
    if (!card || !confirm('Clear all violations?')) return;
    this.adminService.clearCardViolations(card.id).subscribe({
      next: (res) => {
        if (res.success) {
          this.violations.set([]);
        }
      }
    });
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }
}
