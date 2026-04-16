import { Component, inject, signal, HostListener, ElementRef, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../services/auth.service';
import { AdminService, NotificationLog } from '../../../services/admin.service';

type NotificationGroup = {
  label: string;
  items: NotificationLog[];
};

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css'
})
export class NavbarComponent {
  authService = inject(AuthService);
  private adminService = inject(AdminService);
  private eRef = inject(ElementRef);
  private readonly istTimeFormatter = new Intl.DateTimeFormat('en-IN', {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
    timeZone: 'Asia/Kolkata'
  });
  private readonly istMonthFormatter = new Intl.DateTimeFormat('en-IN', {
    month: 'short',
    year: 'numeric',
    timeZone: 'Asia/Kolkata'
  });
  
  user = this.authService.currentUser;
  isProfileOpen = signal(false);
  isNotificationsOpen = signal(false);
  isMobileMenuOpen = signal(false);
  isNotificationsLoading = signal(false);
  notifications = signal<NotificationLog[]>([]);
  groupedNotifications = computed<NotificationGroup[]>(() => {
    const sorted = [...this.notifications()].sort(
      (a, b) => this.parseUtcDate(b.createdAtUtc).getTime() - this.parseUtcDate(a.createdAtUtc).getTime()
    );

    const now = new Date();
    const groups: NotificationGroup[] = [];
    const groupIndex = new Map<string, number>();

    for (const notification of sorted) {
      const label = this.getRelativeGroupLabel(notification.createdAtUtc, now);
      const existingIndex = groupIndex.get(label);

      if (existingIndex === undefined) {
        groupIndex.set(label, groups.length);
        groups.push({ label, items: [notification] });
      } else {
        groups[existingIndex].items.push(notification);
      }
    }

    return groups;
  });

  toggleProfile(): void {
    this.isProfileOpen.update(v => !v);
    if (this.isProfileOpen()) {
      this.isMobileMenuOpen.set(false);
      this.isNotificationsOpen.set(false);
    }
  }

  toggleNotifications(): void {
    const shouldOpen = !this.isNotificationsOpen();

    this.isNotificationsOpen.set(shouldOpen);
    if (shouldOpen) {
      this.isProfileOpen.set(false);
      this.isMobileMenuOpen.set(false);
      this.loadNotifications();
    }
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen.update(v => !v);
    if (this.isMobileMenuOpen()) {
      this.isProfileOpen.set(false);
      this.isNotificationsOpen.set(false);
    }
  }

  loadNotifications(): void {
    this.isNotificationsLoading.set(true);

    const email = this.user()?.email;
    if (!email) {
      this.notifications.set([]);
      this.isNotificationsLoading.set(false);
      return;
    }

    this.adminService.getNotificationLogs({ email, page: 1, pageSize: 50 }).subscribe({
      next: (res) => {
        const payload = res?.data;
        const logs = Array.isArray(payload)
          ? payload
          : Array.isArray(payload?.logs)
            ? payload.logs
            : Array.isArray(payload?.data)
              ? payload.data
              : [];

        this.notifications.set(logs as NotificationLog[]);
        this.isNotificationsLoading.set(false);
      },
      error: () => {
        this.notifications.set([]);
        this.isNotificationsLoading.set(false);
      }
    });
  }

  getNotificationIcon(type: string): string {
    switch (type?.toLowerCase()) {
      case 'paymentcompleted':
        return 'payments';
      case 'paymentfailed':
        return 'error';
      case 'billgenerated':
        return 'receipt_long';
      case 'cardadded':
        return 'credit_card';
      case 'userregistered':
        return 'person_add';
      case 'paymentotpgenerated':
      case 'userotpgenerated':
        return 'password';
      default:
        return 'notifications';
    }
  }

  getNotificationTone(type: string, isSuccess: boolean): string {
    if (!isSuccess) return 'bg-red-50 text-red-600';

    switch (type?.toLowerCase()) {
      case 'paymentcompleted':
        return 'bg-emerald-50 text-emerald-600';
      case 'billgenerated':
        return 'bg-amber-50 text-amber-700';
      case 'cardadded':
        return 'bg-[#f6f3f2] text-[#8a5100]';
      default:
        return 'bg-[#f6f3f2] text-[#615e5c]';
    }
  }

  getNotificationTypeLabel(type: string): string {
    switch (type?.toLowerCase()) {
      case 'paymentcompleted':
        return 'Payment Success';
      case 'paymentfailed':
        return 'Payment Failed';
      case 'billgenerated':
        return 'Bill Generated';
      case 'cardadded':
        return 'Card Added';
      case 'paymentotpgenerated':
        return 'Payment OTP';
      case 'userotpgenerated':
        return 'User OTP';
      case 'userregistered':
        return 'Welcome';
      default:
        return 'Update';
    }
  }

  formatNotificationTime(createdAtUtc: string): string {
    const date = this.parseUtcDate(createdAtUtc);
    if (Number.isNaN(date.getTime())) return '--';
    return this.istTimeFormatter.format(date);
  }

  private getRelativeGroupLabel(createdAtUtc: string, now: Date): string {
    const createdAt = this.parseUtcDate(createdAtUtc);
    if (Number.isNaN(createdAt.getTime())) return 'Recent';

    const dayDiff = this.getDayDifferenceInIst(createdAt, now);

    if (dayDiff <= 0) return 'Today';
    if (dayDiff === 1) return 'Yesterday';
    if (dayDiff <= 7) return 'Earlier This Week';

    return this.istMonthFormatter.format(createdAt);
  }

  private parseUtcDate(value: string): Date {
    if (!value) return new Date(Number.NaN);

    const normalized = value.trim().replace(' ', 'T');
    const hasOffset = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(normalized);
    return new Date(hasOffset ? normalized : `${normalized}Z`);
  }

  private getDayDifferenceInIst(date: Date, compareTo: Date): number {
    const toIstEpochDays = (input: Date): number => {
      const parts = new Intl.DateTimeFormat('en-CA', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        timeZone: 'Asia/Kolkata'
      }).formatToParts(input);

      const year = Number(parts.find((part) => part.type === 'year')?.value);
      const month = Number(parts.find((part) => part.type === 'month')?.value);
      const day = Number(parts.find((part) => part.type === 'day')?.value);

      return Math.floor(Date.UTC(year, month - 1, day) / (1000 * 60 * 60 * 24));
    };

    return toIstEpochDays(compareTo) - toIstEpochDays(date);
  }

  @HostListener('document:click', ['$event'])
  clickout(event: any) {
    if (!this.eRef.nativeElement.contains(event.target)) {
      this.isProfileOpen.set(false);
      this.isNotificationsOpen.set(false);
    }
  }

  logout(): void {
    this.authService.logout();
    this.isProfileOpen.set(false);
    this.isNotificationsOpen.set(false);
  }
}
