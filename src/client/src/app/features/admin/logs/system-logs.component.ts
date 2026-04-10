import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { formatIstDate } from '../../../core/utils/date-time.util';

@Component({
  selector: 'app-system-logs',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './system-logs.component.html'
})
export class SystemLogsComponent implements OnInit {
  private adminService = inject(AdminService);
  Math = Math;
  
  isLoading = signal(true);
  auditLogs = signal<any[]>([]);
  notificationLogs = signal<any[]>([]);
  activeTab = signal<'audit' | 'activity'>('audit');

  // Pagination
  currentPage = signal(1);
  pageSize = signal(20);
  totalAuditLogs = signal(0);
  totalNotificationLogs = signal(0);
  hasMoreLogs = signal(false);

  // Filters (backend only supports traceId for audit, email for notifications)
  traceIdFilter = signal('');
  emailFilter = signal('');

  ngOnInit() {
    this.fetchData();
  }

  fetchData() {
    this.isLoading.set(true);
    
    const params: any = {
      page: this.currentPage(),
      pageSize: this.pageSize()
    };

    if (this.traceIdFilter()) {
      params.traceId = this.traceIdFilter();
    }

    this.adminService.getAuditLogs(params).subscribe({
      next: (res: any) => {
        const data = res.data?.data || res.data || {};
        const logs = Array.isArray(data?.logs) ? data.logs : (Array.isArray(data) ? data : []);

        if (this.currentPage() === 1) {
          this.auditLogs.set(logs);
        } else {
          this.auditLogs.update(current => [...current, ...logs]);
        }

        this.totalAuditLogs.set(data.total || logs.length);
        this.hasMoreLogs.set(logs.length >= this.pageSize());
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to fetch audit logs:', err);
        this.isLoading.set(false);
      }
    });
  }

  fetchNotificationLogs() {
    this.isLoading.set(true);
    
    const params: any = {
      page: this.currentPage(),
      pageSize: this.pageSize()
    };

    if (this.emailFilter()) {
      params.email = this.emailFilter();
    }

    this.adminService.getNotificationLogs(params).subscribe({
      next: (res: any) => {
        const data = res.data?.data || res.data || {};
        const logs = Array.isArray(data?.logs) ? data.logs : (Array.isArray(data) ? data : []);

        if (this.currentPage() === 1) {
          this.notificationLogs.set(logs);
        } else {
          this.notificationLogs.update(current => [...current, ...logs]);
        }

        this.totalNotificationLogs.set(data.total || logs.length);
        this.hasMoreLogs.set(logs.length >= this.pageSize());
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to fetch notification logs:', err);
        this.isLoading.set(false);
      }
    });
  }

  switchTab(tab: 'audit' | 'activity') {
    this.activeTab.set(tab);
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
    this.currentPage.update(p => p + 1);
    if (this.activeTab() === 'audit') {
      this.fetchData();
    } else {
      this.fetchNotificationLogs();
    }
  }

  refresh() {
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
