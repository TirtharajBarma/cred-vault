import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { formatIstDate } from '../../../core/utils/date-time.util';

@Component({
  selector: 'app-admin-statement-detail',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <!-- Back Button -->
      <button (click)="goBack()" class="flex items-center gap-2 text-gray-600 hover:text-gray-900 mb-4">
        <span>←</span> Back to Card
      </button>

      @if (isLoading()) {
        <div class="flex justify-center py-20">
          <div class="animate-spin w-10 h-10 border-4 border-gray-300 border-t-gray-800 rounded-full"></div>
        </div>
      } @else if (statement()) {
        <!-- Statement Header -->
        <div class="bg-gradient-to-r from-blue-800 to-blue-900 rounded-2xl p-5 text-white mb-6">
          <div class="flex flex-col sm:flex-row sm:justify-between sm:items-start gap-3">
            <div class="min-w-0">
              <h1 class="text-xl sm:text-2xl font-bold">Statement Details</h1>
              <p class="text-blue-200 mt-1 text-sm">{{ statement()?.statementPeriod || statement()?.StatementPeriod || '-' }}</p>
              <p class="text-blue-300 text-xs mt-1 truncate">ID: {{ statement()?.id }}</p>
            </div>
            <div class="shrink-0">
              <p class="text-sm text-blue-200">Total Amount</p>
              <p class="text-2xl font-bold">{{ resolvedTotalAmount() | currency }}</p>
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <!-- Left: Statement Info -->
          <div class="lg:col-span-2 space-y-6">
            <!-- Summary -->
            <div class="bg-white rounded-xl border border-gray-200 p-6">
              <h2 class="font-bold text-gray-900 mb-4">Statement Summary</h2>
              <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div class="text-center p-4 bg-gray-50 rounded-xl">
                  <p class="text-sm text-gray-500">Card</p>
                  <p class="font-bold">•••• {{ resolvedCardLast4() }}</p>
                </div>
                <div class="text-center p-4 bg-gray-50 rounded-xl">
                  <p class="text-sm text-gray-500">Network</p>
                  <p class="font-bold">{{ resolvedCardNetwork() }}</p>
                </div>
                <div class="text-center p-4 bg-gray-50 rounded-xl">
                  <p class="text-sm text-gray-500">Issuer</p>
                  <p class="font-bold text-sm">{{ resolvedIssuerName() }}</p>
                </div>
                <div class="text-center p-4 bg-gray-50 rounded-xl">
                  <p class="text-sm text-gray-500">Transactions</p>
                  <p class="font-bold">{{ transactions().length }}</p>
                </div>
              </div>
            </div>

            <!-- Transactions -->
            <div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
              <div class="px-6 py-4 border-b border-gray-200 bg-gray-50">
                <h2 class="font-bold text-gray-900">Transactions ({{ transactions().length }})</h2>
                @if (transactionSource() === 'card') {
                  <p class="text-xs text-gray-500 mt-1">Showing recent card transactions (statement transactions were empty)</p>
                }
              </div>

              @if (isLoadingTransactions()) {
                <div class="p-8 text-center">
                  <div class="animate-spin w-8 h-8 border-4 border-gray-300 border-t-gray-800 rounded-full mx-auto"></div>
                </div>
              } @else if (transactions().length > 0) {
                <div class="max-h-96 overflow-y-auto overflow-x-auto">
                  <table class="w-full min-w-[480px]">
                    <thead class="bg-gray-50">
                      <tr>
                        <th class="text-left px-3 sm:px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Description</th>
                        <th class="text-left px-3 sm:px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Date</th>
                        <th class="text-left px-3 sm:px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Flow</th>
                        <th class="text-right px-3 sm:px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Amount</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-100">
                      @for (txn of pagedTransactions(); track txn.id) {
                        <tr class="hover:bg-gray-50">
                          <td class="px-3 sm:px-6 py-3">
                            <p class="font-medium text-gray-900 text-sm">{{ getTransactionDescription(txn) }}</p>
                          </td>
                          <td class="px-3 sm:px-6 py-3 text-xs text-gray-500 whitespace-nowrap">
                            {{ formatDateTime(txn.transactionDate || txn.dateUtc || txn.DateUtc) }}
                          </td>
                          <td class="px-3 sm:px-6 py-3">
                            <span class="px-2 py-0.5 rounded text-xs font-semibold uppercase tracking-wide"
                                  [class]="getTransactionFlowClass(txn)">
                              {{ getTransactionFlowLabel(txn) }}
                            </span>
                          </td>
                          <td class="px-3 sm:px-6 py-3 text-right">
                            <span [class]="getTransactionAmountClass(txn)"
                                  class="font-semibold text-sm whitespace-nowrap">
                              {{ getTransactionSignedAmount(txn) | currency:'INR' }}
                            </span>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
                @if (transactionTotalPages() > 1) {
                  <div class="flex items-center justify-between px-6 py-3 border-t border-gray-200 bg-gray-50">
                    <span class="text-xs text-gray-500">Page {{ transactionPage() }} of {{ transactionTotalPages() }}</span>
                    <div class="flex items-center gap-2">
                      <button (click)="prevTransactionPage()" [disabled]="transactionPage() === 1"
                              class="px-3 py-1 text-xs bg-white border border-gray-300 rounded hover:bg-gray-100 disabled:opacity-50">
                        Prev
                      </button>
                      <button (click)="nextTransactionPage()" [disabled]="transactionPage() >= transactionTotalPages()"
                              class="px-3 py-1 text-xs bg-gray-800 text-white rounded hover:bg-gray-700 disabled:opacity-50">
                        Next
                      </button>
                    </div>
                  </div>
                }
              } @else {
                <div class="p-8 text-center text-gray-500">
                  <p class="text-2xl mb-2">📋</p>
                  <p>No transactions in this statement</p>
                </div>
              }
            </div>
          </div>

          <!-- Right: Additional Info -->
          <div class="space-y-6">
            <!-- Bill Info -->
            <div class="bg-white rounded-xl border border-gray-200 p-6">
              <h3 class="font-bold text-gray-900 mb-4">Bill Details</h3>
              @if (bill()) {
                <div class="space-y-3 text-sm">
                  <div class="flex justify-between">
                    <span class="text-gray-500">Bill ID</span>
                    <span class="font-mono text-xs">{{ (bill()?.id || bill()?.Id || '').slice(0, 8) }}...</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500">Amount</span>
                    <span class="font-semibold">{{ (bill()?.amount ?? bill()?.Amount ?? 0) | currency }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500">Due Date</span>
                    <span>{{ formatDateTime(bill()?.dueDate || bill()?.dueDateUtc, false) }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500">Status</span>
                    <span class="px-2 py-0.5 rounded text-xs font-semibold"
                          [class]="getBillStatusClass(getEffectiveBillStatus(bill()))">
                      {{ getBillStatusLabel(getEffectiveBillStatus(bill())) }}
                    </span>
                  </div>
                  @if (bill()?.paidAt || bill()?.paidAtUtc) {
                    <div class="flex justify-between">
                      <span class="text-gray-500">Paid At</span>
                      <span>{{ formatDateTime(bill()?.paidAt || bill()?.paidAtUtc) }}</span>
                    </div>
                  }
                </div>
              } @else {
                <p class="text-gray-500 text-sm">Bill information not available</p>
              }
            </div>

            <!-- Rewards Summary -->
            <div class="bg-white rounded-xl border border-gray-200 p-6">
              <h3 class="font-bold text-gray-900 mb-4">Rewards Earned</h3>
              @if (rewards().length > 0) {
                <div class="space-y-3">
                  @for (reward of rewards(); track reward.id) {
                    <div class="flex justify-between items-center">
                      <div>
                        <p class="font-medium text-sm">{{ reward.description || reward.Description || 'Reward' }}</p>
                        <p class="text-xs text-gray-500">{{ reward.type || reward.Type || '-' }}</p>
                      </div>
                      <span class="font-bold text-emerald-600">{{ (reward.points ?? reward.Points ?? reward.amount ?? reward.Amount ?? 0) | currency }}</span>
                    </div>
                  }
                </div>
              } @else {
                <p class="text-gray-500 text-sm">No rewards data</p>
              }
            </div>
          </div>
        </div>
      } @else {
        <div class="text-center py-20">
          <p class="text-gray-500">Statement not found</p>
          <button (click)="goBack()" class="mt-4 px-4 py-2 bg-gray-800 text-white rounded-lg">Go Back</button>
        </div>
      }
    </div>
  `
})
export class AdminStatementDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private adminService = inject(AdminService);

  statement = signal<any>(null);
  cardInfo = signal<any>(null);
  bill = signal<any>(null);
  transactions = signal<any[]>([]);
  transactionPage = signal(1);
  readonly transactionPageSize = 7;
  transactionSource = signal<'statement' | 'card'>('statement');
  rewards = signal<any[]>([]);

  isLoading = signal(true);
  isLoadingTransactions = signal(false);

  userId: string | null = null;
  cardId: string | null = null;
  statementId: string | null = null;

  ngOnInit() {
    this.userId = this.route.snapshot.paramMap.get('userId');
    this.cardId = this.route.snapshot.paramMap.get('cardId');
    this.statementId = this.route.snapshot.paramMap.get('statementId');
    
    if (this.statementId) {
      this.loadFullDetails(this.statementId);
    }

    if (this.userId && this.cardId) {
      this.loadCardInfo(this.userId, this.cardId);
    }
  }

  loadCardInfo(userId: string, cardId: string) {
    this.adminService.getCardsByUser(userId).subscribe({
      next: (res) => {
        const cards = this.extractArray(res, ['data', 'cards']);
        const card = (Array.isArray(cards) ? cards : []).find((c: any) => c?.id === cardId);
        this.cardInfo.set(card || null);
      }
    });
  }

  loadFullDetails(statementId: string) {
    this.isLoading.set(true);
    this.isLoadingTransactions.set(true);

    this.adminService.getAdminStatementFull(statementId).subscribe({
      next: (res) => {
        const payload = res.data?.data || res.data || res;

        this.statement.set(payload?.statement || payload?.Statement || null);
        this.bill.set(payload?.bill || payload?.Bill || null);
        this.transactions.set(this.extractArray(payload?.transactions || payload?.Transactions, ['data', 'transactions']));
        this.transactionPage.set(1);
        this.rewards.set(this.extractArray(payload?.rewards || payload?.Rewards, ['data', 'rewards']));
        this.transactionSource.set('statement');

        if (this.transactions().length === 0 && this.cardId) {
          this.loadCardTransactionFallback();
          return;
        }

        this.isLoading.set(false);
        this.isLoadingTransactions.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.isLoadingTransactions.set(false);
      }
    });
  }

  loadCardTransactionFallback() {
    if (!this.cardId) {
      this.isLoading.set(false);
      this.isLoadingTransactions.set(false);
      return;
    }

    this.adminService.getAdminCardTransactions(this.cardId).subscribe({
      next: (cardRes) => {
        const cardTxns = this.extractArray(cardRes, ['data', 'transactions']);
        this.transactions.set(cardTxns.slice(0, 20));
        this.transactionPage.set(1);
        this.transactionSource.set('card');
        this.isLoading.set(false);
        this.isLoadingTransactions.set(false);
      },
      error: () => {
        this.transactions.set([]);
        this.isLoading.set(false);
        this.isLoadingTransactions.set(false);
      }
    });
  }

  resolvedCardLast4(): string {
    return this.statement()?.cardLast4 || this.statement()?.CardLast4 || this.cardInfo()?.last4 || '----';
  }

  resolvedCardNetwork(): string {
    return this.statement()?.cardNetwork || this.statement()?.CardNetwork || this.cardInfo()?.network || '-';
  }

  resolvedIssuerName(): string {
    const issuerFromStatement = this.statement()?.issuerName || this.statement()?.IssuerName;
    const looksLikeGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(issuerFromStatement || ''));
    if (!issuerFromStatement || looksLikeGuid) {
      return this.cardInfo()?.issuerName || '-';
    }
    return issuerFromStatement;
  }

  resolvedTotalAmount(): number {
    const statement = this.statement() || {};
    const bill = this.bill() || {};

    const candidates = [
      statement.totalAmount,
      statement.TotalAmount,
      statement.totalPurchases,
      statement.TotalPurchases,
      bill.amount,
      bill.Amount,
      statement.closingBalance,
      statement.ClosingBalance
    ];

    for (const value of candidates) {
      const n = Number(value);
      if (Number.isFinite(n) && n > 0) {
        return n;
      }
    }

    const transactionSum = this.transactions().reduce((sum, txn) => {
      const amount = Number(txn?.amount ?? txn?.Amount ?? 0);
      return Number.isFinite(amount) ? sum + Math.abs(amount) : sum;
    }, 0);

    return transactionSum;
  }

  transactionTotalPages(): number {
    return Math.max(1, Math.ceil(this.transactions().length / this.transactionPageSize));
  }

  pagedTransactions(): any[] {
    const start = (this.transactionPage() - 1) * this.transactionPageSize;
    return this.transactions().slice(start, start + this.transactionPageSize);
  }

  prevTransactionPage() {
    if (this.transactionPage() > 1) {
      this.transactionPage.update((page) => page - 1);
    }
  }

  nextTransactionPage() {
    if (this.transactionPage() < this.transactionTotalPages()) {
      this.transactionPage.update((page) => page + 1);
    }
  }

  extractArray(source: any, keys: string[] = []): any[] {
    const seen = new Set<any>();
    const queue: any[] = [source];

    while (queue.length > 0) {
      const current = queue.shift();
      if (current == null || seen.has(current)) continue;
      seen.add(current);

      if (Array.isArray(current)) return current;
      if (Array.isArray(current?.$values)) return current.$values;

      for (const key of keys) {
        if (Array.isArray(current?.[key])) return current[key];
        if (Array.isArray(current?.[key]?.$values)) return current[key].$values;
      }

      if (typeof current === 'object') {
        for (const value of Object.values(current)) {
          if (value && (Array.isArray(value) || typeof value === 'object')) {
            queue.push(value);
          }
        }
      }
    }

    return [];
  }

  goBack() {
    this.router.navigate(['/admin/users', this.userId, 'cards', this.cardId]);
  }

  formatDateTime(dateStr: string, includeTime = true): string {
    if (!dateStr) return '-';
    const formatted = includeTime
      ? formatIstDate(dateStr, 'MMM d, y, hh:mm a', '-')
      : formatIstDate(dateStr, 'MMM d, y', '-');

    return includeTime ? `${formatted} IST` : formatted;
  }

  getEffectiveBillStatus(billObj: any): number {
    if (!billObj) return 0;
    
    const amount = Number(billObj.amount ?? billObj.Amount ?? 0);
    const amountPaid = Number(billObj.amountPaid ?? billObj.AmountPaid ?? 0);
    const status = Number(billObj.status ?? billObj.Status ?? 0);
    const dueDateStr = billObj.dueDate ?? billObj.dueDateUtc ?? billObj.DueDate;

    const outstanding = Math.max(0, amount - amountPaid);
    if (outstanding <= 0 && amount > 0) return 1; // Paid

    if (dueDateStr) {
      const dueDate = new Date(dueDateStr);
      const now = new Date();
      if (dueDate.getTime() < now.getTime() && outstanding > 0) return 2; // Overdue
    }

    if (amountPaid > 0) return 4; // Partially Paid

    return status;
  }

  getBillStatusLabel(status: number): string {
    const labels: Record<number, string> = {
      0: 'Pending', 1: 'Paid', 2: 'Overdue', 3: 'Cancelled', 4: 'Partially Paid'
    };
    return labels[status] || 'Unknown';
  }

  getBillStatusClass(status: number): string {
    const classes: Record<number, string> = {
      0: 'bg-amber-100 text-amber-700',
      1: 'bg-emerald-100 text-emerald-700',
      2: 'bg-red-100 text-red-700',
      3: 'bg-gray-100 text-gray-700',
      4: 'bg-blue-100 text-blue-700'
    };
    return classes[status] || 'bg-gray-100 text-gray-700';
  }

  getTransactionDescription(txn: any): string {
    const raw = String(txn?.description || txn?.Description || 'Transaction').trim();
    if (!raw) return 'Transaction';

    if (raw.startsWith('Saga:')) {
      return 'Payment Credit (Saga)';
    }

    if (raw.toLowerCase().startsWith('bill payment:')) {
      return 'Bill Payment Credit';
    }

    return raw;
  }

  getTransactionFlowLabel(txn: any): string {
    return this.isCreditTransaction(txn) ? 'Credit' : 'Debit';
  }

  getTransactionFlowClass(txn: any): string {
    return this.isCreditTransaction(txn)
      ? 'bg-emerald-100 text-emerald-700'
      : 'bg-red-100 text-red-700';
  }

  getTransactionAmountClass(txn: any): string {
    return this.isCreditTransaction(txn) ? 'font-semibold text-emerald-600' : 'font-semibold text-red-600';
  }

  getTransactionSignedAmount(txn: any): number {
    const amount = Math.abs(Number(txn?.amount ?? txn?.Amount ?? 0));
    return this.isCreditTransaction(txn) ? amount : -amount;
  }

  private isCreditTransaction(txn: any): boolean {
    const amount = Number(txn?.amount ?? txn?.Amount ?? 0);
    if (amount < 0) return true;

    const rawType = String(txn?.type ?? txn?.Type ?? '').toLowerCase();
    if (rawType === '2' || rawType === '3' || rawType.includes('payment') || rawType.includes('refund') || rawType.includes('credit')) {
      return true;
    }

    const description = String(txn?.description || txn?.Description || '').toLowerCase();
    return description.startsWith('saga:') || description.startsWith('bill payment:');
  }
}
