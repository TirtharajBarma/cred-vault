import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-user-detail',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <!-- Back Button -->
      <button (click)="goBack()" class="flex items-center gap-2 text-gray-600 hover:text-gray-900 mb-4">
        <span>←</span> Back to Users
      </button>

      @if (isLoading()) {
        <div class="flex justify-center py-20">
          <div class="animate-spin w-10 h-10 border-4 border-gray-300 border-t-gray-800 rounded-full"></div>
        </div>
      } @else if (user()) {
        <!-- User Header -->
        <div class="bg-gradient-to-r from-gray-800 to-gray-900 rounded-2xl p-6 text-white mb-6">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-4">
              <div class="w-16 h-16 bg-white/20 rounded-2xl flex items-center justify-center text-3xl font-bold">
                {{ user()?.fullName?.charAt(0) || '?' }}
              </div>
              <div>
                <h1 class="text-2xl font-bold">{{ user()?.fullName }}</h1>
                <p class="text-gray-300">{{ user()?.email }}</p>
                <p class="text-gray-400 text-sm mt-1">ID: {{ user()?.id }}</p>
              </div>
            </div>
            <div class="text-right">
              <span class="px-4 py-1.5 rounded-full text-sm font-semibold bg-white/20">
                {{ getStatusLabel(user()?.status) }}
              </span>
              <p class="text-gray-300 mt-2">{{ user()?.role }}</p>
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- Left: Cards -->
          <div class="lg:col-span-7">
            <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
              <div class="px-6 py-4 border-b border-gray-200 flex justify-between items-center bg-gray-50">
                <h2 class="font-bold text-gray-900">Cards ({{ cards().length }})</h2>
              </div>

              @if (isLoadingCards()) {
                <div class="p-8 text-center">
                  <div class="animate-spin w-8 h-8 border-4 border-gray-300 border-t-gray-800 rounded-full mx-auto"></div>
                </div>
              } @else if (cards().length > 0) {
                <div class="divide-y divide-gray-100">
                  @for (card of cards(); track card.id) {
                    <div (click)="viewCard(card)" 
                         class="p-6 hover:bg-blue-50 cursor-pointer transition-all">
                      <div class="flex justify-between items-start">
                        <div>
                          <div class="flex items-center gap-3 mb-2">
                            <span class="text-2xl">{{ card.network === 'Visa' ? '💳' : '💠' }}</span>
                            <div>
                              <p class="font-bold text-lg text-gray-900">{{ card.issuerName || 'Credit Card' }}</p>
                              <p class="text-gray-500">{{ card.network }} •••• {{ card.last4 }}</p>
                            </div>
                          </div>
                          <div class="flex gap-4 text-sm">
                            <div>
                              <span class="text-gray-500">Credit Limit:</span>
                              <span class="ml-1 font-semibold">{{ card.creditLimit | currency }}</span>
                            </div>
                            <div>
                              <span class="text-gray-500">Outstanding:</span>
                              <span class="ml-1 font-semibold text-red-600">{{ card.outstandingBalance | currency }}</span>
                            </div>
                          </div>
                        </div>
                        <div class="text-right">
                          @if (card.isBlocked) {
                            <span class="px-3 py-1 bg-red-500 text-white rounded-lg text-sm font-bold">BLOCKED</span>
                          } @else if ((card.creditLimit ?? 0) <= 0) {
                            <span class="px-3 py-1 bg-amber-500 text-white rounded-lg text-sm font-bold">SETUP</span>
                          } @else {
                            <span class="px-3 py-1 bg-emerald-500 text-white rounded-lg text-sm font-bold">ACTIVE</span>
                          }
                          <p class="text-gray-400 text-sm mt-2">→ View Details</p>
                        </div>
                      </div>
                    </div>
                  }
                </div>
              } @else {
                <div class="text-center py-12 text-gray-500">
                  <p class="text-4xl mb-2">💳</p>
                  <p>No cards found</p>
                </div>
              }
            </div>
          </div>

          <!-- Right: User Info & Logs -->
          <div class="lg:col-span-5 space-y-4 lg:sticky lg:top-24 self-start">
            <!-- User Actions -->
            <div class="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
              <div class="flex items-center gap-2 mb-4">
                <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
                <h3 class="font-bold text-gray-900">Change Status</h3>
              </div>
              <div class="grid grid-cols-2 gap-2">
                <button (click)="updateStatus('active')" 
                        [disabled]="user()?.status === 'active'"
                        [class]="user()?.status === 'active' ? 'px-3 py-2 bg-emerald-100 text-emerald-700 rounded-lg text-sm font-medium border-2 border-emerald-300 cursor-not-allowed' : 'px-3 py-2 bg-emerald-500 text-white rounded-lg text-sm font-medium hover:bg-emerald-600'">
                  Active
                </button>
                <button (click)="updateStatus('suspended')" 
                        [disabled]="user()?.status === 'suspended'"
                        [class]="user()?.status === 'suspended' ? 'px-3 py-2 bg-orange-100 text-orange-700 rounded-lg text-sm font-medium border-2 border-orange-300 cursor-not-allowed' : 'px-3 py-2 bg-orange-500 text-white rounded-lg text-sm font-medium hover:bg-orange-600'">
                  Suspended
                </button>
                <button (click)="updateStatus('blocked')" 
                        [disabled]="user()?.status === 'blocked'"
                        [class]="user()?.status === 'blocked' ? 'px-3 py-2 bg-red-100 text-red-700 rounded-lg text-sm font-medium border-2 border-red-300 cursor-not-allowed' : 'px-3 py-2 bg-red-500 text-white rounded-lg text-sm font-medium hover:bg-red-600'">
                  Blocked
                </button>
                <button (click)="updateStatus('pendingverification')" 
                        [disabled]="user()?.status === 'pendingverification'"
                        [class]="user()?.status === 'pendingverification' ? 'px-3 py-2 bg-amber-100 text-amber-700 rounded-lg text-sm font-medium border-2 border-amber-300 cursor-not-allowed' : 'px-3 py-2 bg-amber-500 text-white rounded-lg text-sm font-medium hover:bg-amber-600'">
                  Pending
                </button>
              </div>
              
              <div class="mt-4 pt-4 border-t border-gray-200">
                <button (click)="toggleRole()"
                        [disabled]="user()?.role === 'admin'"
                        [class]="user()?.role === 'admin' ? 'w-full px-4 py-2 bg-amber-100 text-amber-700 border border-amber-300 rounded-lg text-sm font-medium cursor-not-allowed' : 'w-full px-4 py-2 bg-amber-500 text-white rounded-lg text-sm font-medium hover:bg-amber-600'">
                  {{ user()?.role === 'admin' ? 'Already Admin' : 'Make Admin' }}
                </button>
              </div>
            </div>

            <!-- Notification Logs -->
            <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
              <div class="px-5 py-3 border-b border-emerald-100 bg-emerald-50/60">
                <div class="flex justify-between items-center">
                  <h3 class="font-bold text-gray-900">Notifications</h3>
                  <span class="px-2 py-0.5 rounded-md text-xs font-semibold bg-emerald-100 text-emerald-700">{{ logs().length }}</span>
                </div>
              </div>
              @if (isLoadingLogs()) {
                <div class="h-64 p-6 text-center">
                  <div class="animate-spin w-6 h-6 border-3 border-gray-300 border-t-gray-800 rounded-full mx-auto"></div>
                </div>
              } @else if (logs().length > 0) {
                <div class="h-72 overflow-y-auto overscroll-contain scroll-smooth divide-y divide-gray-100">
                  @for (log of logs(); track log.id) {
                    <div class="px-4 py-2.5 hover:bg-gray-50 transition-all">
                      <div class="flex items-start gap-2.5">
                        @if (log.isSuccess) {
                          <span class="mt-0.5 text-emerald-500">✓</span>
                        } @else {
                          <span class="mt-0.5 text-red-500">✗</span>
                        }
                        <div class="flex-1 min-w-0">
                          <p class="text-sm font-medium text-gray-900 truncate">{{ log.subject }}</p>
                          <p class="text-xs text-gray-500 truncate">{{ getNotificationPurpose(log) }} • {{ formatDate(log.createdAtUtc) }}</p>
                          <p class="text-xs text-gray-400 truncate">{{ getNotificationContext(log) }}</p>
                        </div>
                      </div>
                    </div>
                  }
                </div>
              } @else {
                <div class="h-72 p-6 text-center text-gray-500 text-sm">No notifications</div>
              }
            </div>

            <!-- Audit Logs -->
            <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
              <div class="px-5 py-3 border-b border-slate-100 bg-slate-50/80">
                <div class="flex justify-between items-center">
                  <h3 class="font-bold text-gray-900">Activity</h3>
                  <span class="px-2 py-0.5 rounded-md text-xs font-semibold bg-slate-100 text-slate-700">{{ auditLogs().length }}</span>
                </div>
              </div>
              @if (isLoadingAudit()) {
                <div class="h-64 p-6 text-center">
                  <div class="animate-spin w-6 h-6 border-3 border-gray-300 border-t-gray-800 rounded-full mx-auto"></div>
                </div>
              } @else if (auditLogs().length > 0) {
                <div class="h-72 overflow-y-auto overscroll-contain scroll-smooth divide-y divide-gray-100">
                  @for (audit of auditLogs(); track audit.id) {
                    <div class="px-4 py-2.5 hover:bg-gray-50 transition-all">
                      <p class="text-sm font-medium text-gray-900 truncate">{{ audit.action }}</p>
                      <p class="text-xs text-gray-500 truncate">{{ getAuditActor(audit) }} • {{ formatDate(audit.createdAtUtc) }}</p>
                      <p class="text-xs text-gray-400 truncate">{{ getAuditContext(audit) }}</p>
                    </div>
                  }
                </div>
              } @else {
                <div class="h-72 p-6 text-center text-gray-500 text-sm">No activity</div>
              }
            </div>
          </div>
        </div>
      } @else {
        <div class="text-center py-20">
          <p class="text-gray-500">User not found</p>
          <button (click)="goBack()" class="mt-4 px-4 py-2 bg-gray-800 text-white rounded-lg">Go Back</button>
        </div>
      }
    </div>
  `
})
export class UserDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private adminService = inject(AdminService);

  user = signal<any>(null);
  cards = signal<any[]>([]);
  logs = signal<any[]>([]);
  auditLogs = signal<any[]>([]);

  isLoading = signal(true);
  isLoadingCards = signal(false);
  isLoadingLogs = signal(false);
  isLoadingAudit = signal(false);

  ngOnInit() {
    const userId = this.route.snapshot.paramMap.get('id');
    if (userId) {
      this.loadUser(userId);
      this.loadCards(userId);
      this.loadAudit(userId);
    }
  }

  loadUser(userId: string) {
    this.adminService.getUserDetails(userId).subscribe({
      next: (res: any) => {
        const user = res?.data?.data?.user || res?.data?.user || res?.data;
        this.user.set(user);
        this.loadLogs();
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  loadCards(userId: string) {
    this.isLoadingCards.set(true);
    this.adminService.getCardsByUser(userId).subscribe({
      next: (res) => {
        this.cards.set(res.data?.data || res.data || []);
        this.isLoadingCards.set(false);
      },
      error: () => this.isLoadingCards.set(false)
    });
  }

  loadLogs() {
    this.isLoadingLogs.set(true);
    const user = this.user();
    if (user?.email) {
      this.adminService.getUserNotificationLogs(user.email, 1, 200).subscribe({
        next: (res) => {
          const data = res.data?.data || res.data || res;
          const rows = Array.isArray(data?.logs) ? data.logs : (Array.isArray(data) ? data : []);
          this.logs.set(rows);
          this.isLoadingLogs.set(false);
        },
        error: () => this.isLoadingLogs.set(false)
      });
    } else {
      this.isLoadingLogs.set(false);
    }
  }

  loadAudit(userId: string) {
    this.isLoadingAudit.set(true);
    this.adminService.getUserAuditLogs(userId, 1, 200).subscribe({
      next: (res) => {
        const data = res.data?.data || res.data || res;
        const rows = Array.isArray(data?.logs) ? data.logs : (Array.isArray(data) ? data : []);
        this.auditLogs.set(rows);
        this.isLoadingAudit.set(false);
      },
      error: () => this.isLoadingAudit.set(false)
    });
  }

  viewCard(card: any) {
    this.router.navigate(['/admin/users', this.user()?.id, 'cards', card.id]);
  }

  goBack() {
    this.router.navigate(['/admin/users']);
  }

  updateStatus(status: string) {
    const user = this.user();
    if (!user) return;
    this.adminService.updateUserStatus(user.id, status).subscribe({
      next: (res) => {
        if (res.success) {
          this.user.set({ ...user, status });
        }
      }
    });
  }

  toggleRole() {
    const user = this.user();
    if (!user) return;
    const newRole = user.role === 'admin' ? 'user' : 'admin';
    this.adminService.updateUserRole(user.id, newRole).subscribe({
      next: (res) => {
        if (res.success) {
          this.user.set({ ...user, role: newRole });
        }
      }
    });
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      'active': 'Active', 'suspended': 'Suspended',
      'pendingverification': 'Pending', 'blocked': 'Blocked'
    };
    return labels[status?.toLowerCase()] || status || 'Unknown';
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '-';
    return `${new Intl.DateTimeFormat('en-IN', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: true,
      timeZone: 'Asia/Kolkata'
    }).format(new Date(dateStr))} IST`;
  }

  getNotificationPurpose(log: any): string {
    const payload = this.parseJson(log?.body);
    const subject = String(log?.subject || 'Notification').toLowerCase();
    if (subject.includes('verification')) return 'OTP Verification';
    if (subject.includes('successful')) return 'Payment Completed';
    if (subject.includes('failed')) return 'Payment Failure';
    return String(payload?.Purpose || payload?.purpose || log?.type || 'Event');
  }

  getNotificationContext(log: any): string {
    const payload = this.parseJson(log?.body);
    const paymentId = payload?.PaymentId || payload?.paymentId;
    const billId = payload?.BillId || payload?.billId;
    const amount = Number(payload?.Amount ?? payload?.amount ?? 0);
    const parts: string[] = [];

    if (paymentId) parts.push(`Payment ${String(paymentId).slice(0, 8)}`);
    if (billId) parts.push(`Bill ${String(billId).slice(0, 8)}`);
    if (Number.isFinite(amount) && amount > 0) parts.push(`Amount INR ${amount.toFixed(2)}`);
    if (log?.recipient) parts.push(`To ${log.recipient}`);

    return parts.join(' | ') || 'No additional context';
  }

  getAuditActor(audit: any): string {
    return audit?.userId || audit?.UserId || audit?.entityName || 'System';
  }

  getAuditContext(audit: any): string {
    const changes = this.parseJson(audit?.changes);
    const reason = changes?.Reason || changes?.reason;
    const amount = Number(changes?.Amount ?? changes?.amount ?? 0);
    const summary: string[] = [];

    if (audit?.entityName) summary.push(String(audit.entityName));
    if (Number.isFinite(amount) && amount > 0) summary.push(`Amount INR ${amount.toFixed(2)}`);
    if (reason) summary.push(`Reason: ${reason}`);

    return summary.join(' | ') || 'No contextual details';
  }

  private parseJson(value: any): any {
    if (!value) return null;
    if (typeof value === 'object') return value;
    try {
      return JSON.parse(value);
    } catch {
      return null;
    }
  }
}
