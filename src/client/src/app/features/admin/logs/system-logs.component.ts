import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { formatIstDate } from '../../../core/utils/date-time.util';

@Component({
  selector: 'app-system-logs',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './system-logs.component.html',
})
export class SystemLogsComponent implements OnInit {
  private adminService = inject(AdminService);
  private router = inject(Router);
  Math = Math;

  isLoading = signal(true);
  auditLogs = signal<any[]>([]);
  notificationLogs = signal<any[]>([]);
  activeTab = signal<'audit' | 'activity'>('audit');

  private userCache = new Map<string, string>();

  // Pagination
  currentPage = signal(1);
  pageSize = signal(20);
  totalAuditLogs = signal(0);
  totalNotificationLogs = signal(0);
  hasMoreLogs = signal(false);

  // Filters (backend only supports traceId for audit, email for notifications)
  traceIdFilter = signal('');
  emailFilter = signal('');
  selectedLog = signal<any | null>(null);
  selectedLogType = signal<'audit' | 'activity' | null>(null);

  ngOnInit() {
    this.fetchData();
  }

  fetchData() {
    this.isLoading.set(true);

    const params: any = {
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };

    if (this.traceIdFilter()) {
      params.traceId = this.traceIdFilter();
    }

    this.adminService.getAuditLogs(params).subscribe({
      next: (res: any) => {
        const data = res.data?.data || res.data || {};
        const logs = Array.isArray(data?.logs) ? data.logs : Array.isArray(data) ? data : [];

        if (this.currentPage() === 1) {
          this.auditLogs.set(logs);
        } else {
          this.auditLogs.update((current) => [...current, ...logs]);
        }

        this.totalAuditLogs.set(data.total || logs.length);
        this.hasMoreLogs.set(logs.length >= this.pageSize());
        this.prefetchUserNames(logs);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to fetch audit logs:', err);
        this.isLoading.set(false);
      },
    });
  }

  private prefetchUserNames(logs: any[]) {
    const userIds = new Set<string>();

    for (const log of logs) {
      const userId = log?.userId || log?.UserId;
      if (userId && this.isGuid(userId) && !this.userCache.has(userId)) {
        userIds.add(userId);
      }
    }

    console.log('[SystemLogs] Prefetching userIds:', Array.from(userIds));
    if (userIds.size === 0) return;

    for (const userId of userIds) {
      this.adminService.getUserDetails(userId).subscribe({
        next: (res: any) => {
          const user = res?.data?.data || res?.data;
          const displayName = user?.fullName || user?.email || userId;
          console.log('[SystemLogs] Cached user:', userId, '->', displayName);
          this.userCache.set(userId, displayName);
        },
        error: () => {
          this.userCache.set(userId, userId);
        },
      });
    }
  }

  private isGuid(value: unknown): boolean {
    if (!value) return false;
    const text = String(value).trim();
    const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
    return guidRegex.test(text);
  }

  fetchNotificationLogs() {
    this.isLoading.set(true);

    const params: any = {
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };

    if (this.emailFilter()) {
      params.email = this.emailFilter();
    }

    this.adminService.getNotificationLogs(params).subscribe({
      next: (res: any) => {
        const data = res.data?.data || res.data || {};
        const logs = Array.isArray(data?.logs) ? data.logs : Array.isArray(data) ? data : [];

        if (this.currentPage() === 1) {
          this.notificationLogs.set(logs);
        } else {
          this.notificationLogs.update((current) => [...current, ...logs]);
        }

        this.totalNotificationLogs.set(data.total || logs.length);
        this.hasMoreLogs.set(logs.length >= this.pageSize());
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to fetch notification logs:', err);
        this.isLoading.set(false);
      },
    });
  }

  switchTab(tab: 'audit' | 'activity') {
    this.activeTab.set(tab);
    this.clearLogDetail();
    this.currentPage.set(1);
    this.auditLogs.set([]);
    this.notificationLogs.set([]);

    if (tab === 'activity') {
      this.fetchNotificationLogs();
    } else {
      this.fetchData();
    }
  }

  onSearch() {
    this.clearLogDetail();
    this.currentPage.set(1);
    if (this.activeTab() === 'audit') {
      this.fetchData();
    } else {
      this.fetchNotificationLogs();
    }
  }

  clearFilters() {
    this.traceIdFilter.set('');
    this.emailFilter.set('');
    this.currentPage.set(1);
    if (this.activeTab() === 'audit') {
      this.fetchData();
    } else {
      this.fetchNotificationLogs();
    }
  }

  loadMore() {
    this.currentPage.update((p) => p + 1);
    if (this.activeTab() === 'audit') {
      this.fetchData();
    } else {
      this.fetchNotificationLogs();
    }
  }

  refresh() {
    this.clearLogDetail();
    this.currentPage.set(1);
    if (this.activeTab() === 'audit') {
      this.fetchData();
    } else {
      this.fetchNotificationLogs();
    }
  }

  hasActiveFilters(): boolean {
    return !!(this.traceIdFilter() || this.emailFilter());
  }

  getTotalCount(): number {
    return this.activeTab() === 'audit' ? this.totalAuditLogs() : this.totalNotificationLogs();
  }

  getCurrentLogs(): any[] {
    return this.activeTab() === 'audit' ? this.auditLogs() : this.notificationLogs();
  }

  formatDateTime(dateStr: string): string {
    if (!dateStr) return '-';
    return `${formatIstDate(dateStr, 'MMM d, y, hh:mm a', '-')} IST`;
  }

  getAuditEventTitle(log: any): string {
    const entity = log?.entityName || 'SystemEvent';
    const action = log?.action || 'Processed';
    return `${entity} ${action}`;
  }

  getAuditActor(log: any): string {
    const changes = this.parseJson(log?.changes);
    return changes?.UserId || changes?.userId || changes?.Actor || changes?.actor || 'System';
  }

  getAuditUser(log: any): string {
    const changes = this.parseJson(log?.changes);
    const fullName = this.pickFirst(changes, ['FullName', 'fullName', 'Name', 'name']);
    const email = this.pickFirst(changes, ['Email', 'email']);
    const userId = this.pickFirst(changes, ['UserId', 'userId']) || log?.userId;

    if (fullName) return fullName;
    if (email) return email;
    if (userId) {
      const cachedName = this.userCache.get(userId);
      if (cachedName) return cachedName;
      return String(userId);
    }

    return 'System';
  }

  getAuditContext(log: any): string {
    const changes = this.parseJson(log?.changes);
    const parts: string[] = [];

    const paymentId = changes?.PaymentId || changes?.paymentId;
    const billId = changes?.BillId || changes?.billId;
    const amount = Number(changes?.Amount ?? changes?.amount ?? 0);
    const reason = changes?.Reason || changes?.reason;

    if (paymentId) parts.push(`Payment ${String(paymentId).slice(0, 8)}`);
    if (billId) parts.push(`Bill ${String(billId).slice(0, 8)}`);
    if (Number.isFinite(amount) && amount > 0) parts.push(`Amount INR ${amount.toFixed(2)}`);
    if (reason) parts.push(`Reason: ${reason}`);

    return parts.join(' | ') || 'No detailed context';
  }

  getNotificationTitle(log: any): string {
    const subject = String(log?.subject || '').toLowerCase();
    if (subject.includes('verification')) return 'OTP Verification';
    if (subject.includes('successful')) return 'Payment Completed';
    if (subject.includes('failed')) return 'Payment Failed';
    return log?.subject || 'Notification';
  }

  getNotificationUser(log: any): string {
    const payload = this.parseJson(log?.body);
    const fullName = this.pickFirst(payload, ['FullName', 'fullName', 'Name', 'name']);
    const email = this.pickFirst(payload, ['Email', 'email']) || log?.recipient;
    const userId = this.pickFirst(payload, ['UserId', 'userId']);

    if (fullName) return fullName;
    if (email) return String(email);
    if (userId) return String(userId);

    return 'Unknown user';
  }

  getNotificationContext(log: any): string {
    const payload = this.parseJson(log?.body);
    const parts: string[] = [];
    const paymentId = payload?.PaymentId || payload?.paymentId;
    const billId = payload?.BillId || payload?.billId;
    const amount = Number(payload?.Amount ?? payload?.amount ?? 0);
    const otp = payload?.OtpCode || payload?.otpCode;

    if (paymentId) parts.push(`Payment ${String(paymentId).slice(0, 8)}`);
    if (billId) parts.push(`Bill ${String(billId).slice(0, 8)}`);
    if (Number.isFinite(amount) && amount > 0) parts.push(`Amount INR ${amount.toFixed(2)}`);
    if (otp) parts.push('Contains OTP');

    return parts.join(' | ') || 'No contextual payload';
  }

  openAuditDetail(log: any) {
    this.selectedLogType.set('audit');
    this.selectedLog.set(log);
  }

  openActivityDetail(log: any) {
    this.selectedLogType.set('activity');
    this.selectedLog.set(log);
  }

  clearLogDetail() {
    this.selectedLog.set(null);
    this.selectedLogType.set(null);
  }

  getDetailRows(): Array<{ label: string; value: string }> {
    const log = this.selectedLog();
    const type = this.selectedLogType();

    if (!log || !type) {
      return [];
    }

    if (type === 'audit') {
      return [
        { label: 'Event', value: this.getAuditEventTitle(log) },
        { label: 'User', value: this.getAuditUser(log) },
        { label: 'Action', value: log?.action || '-' },
        { label: 'Actor', value: this.getAuditActor(log) },
        { label: 'Entity', value: log?.entityName || '-' },
        { label: 'Entity ID', value: log?.entityId || '-' },
        { label: 'Trace ID', value: log?.traceId || '-' },
        { label: 'Time', value: this.formatDateTime(log?.createdAtUtc) },
        { label: 'Context', value: this.getAuditContext(log) },
      ];
    }

    return [
      { label: 'Event', value: this.getNotificationTitle(log) },
      { label: 'User', value: this.getNotificationUser(log) },
      { label: 'Status', value: log?.isSuccess ? 'Success' : 'Failed' },
      { label: 'Recipient', value: log?.recipient || '-' },
      { label: 'Type', value: log?.type || '-' },
      { label: 'Trace ID', value: log?.traceId || '-' },
      { label: 'Time', value: this.formatDateTime(log?.createdAtUtc) },
      { label: 'Context', value: this.getNotificationContext(log) },
      { label: 'Error', value: log?.errorMessage || '-' },
    ];
  }

  canOpenSelectedUser(): boolean {
    return !!this.getSelectedUserId();
  }

  openSelectedUser(): void {
    const userId = this.getSelectedUserId();
    if (!userId) return;
    this.router.navigate(['/admin/users', userId]);
  }

  getSelectedUserId(): string | null {
    const log = this.selectedLog();
    const type = this.selectedLogType();

    if (!log || !type) return null;

    if (type === 'audit') {
      const changes = this.parseJson(log?.changes);
      const id = this.pickFirst(changes, ['UserId', 'userId']) || log?.userId;
      return this.asGuid(id);
    }

    const payload = this.parseJson(log?.body);
    const id = this.pickFirst(payload, ['UserId', 'userId']);
    return this.asGuid(id);
  }

  getSelectedRawPayload(): string {
    const log = this.selectedLog();
    const type = this.selectedLogType();

    if (!log || !type) {
      return '{}';
    }

    const payload = type === 'audit' ? this.parseJson(log?.changes) : this.parseJson(log?.body);

    if (!payload) {
      return '{}';
    }

    try {
      return JSON.stringify(payload, null, 2);
    } catch {
      return '{}';
    }
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

  private pickFirst(source: any, keys: string[]): string | null {
    if (!source || typeof source !== 'object') return null;

    for (const key of keys) {
      const value = source?.[key];
      if (value !== undefined && value !== null && String(value).trim()) {
        return String(value).trim();
      }
    }

    return null;
  }

  private asGuid(value: unknown): string | null {
    if (!value) return null;
    const text = String(value).trim();
    const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
    return guidRegex.test(text) ? text : null;
  }
}
