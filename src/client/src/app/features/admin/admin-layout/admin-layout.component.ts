import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';
import { AdminService } from '../../../core/services/admin.service';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.css'
})
export class AdminLayoutComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private router = inject(Router);
  private adminService = inject(AdminService);
  private refreshHandle: ReturnType<typeof setInterval> | null = null;
  private alertsSub: Subscription | null = null;
  private refreshAlertsHandler = () => this.loadPendingCardAlerts();
  
  user = this.authService.currentUser;
  isSidebarOpen = signal(true);
  showAlerts = signal(false);
  isLoadingAlerts = signal(false);
  pendingCardAlerts = signal<Array<{ userId: string; userName: string; userEmail: string; cardId: string; last4: string; issuerName: string }>>([]);
  groupedPendingAlerts = computed(() => {
    const groups = new Map<string, { userId: string; userName: string; userEmail: string; cards: Array<{ cardId: string; last4: string; issuerName: string }> }>();

    for (const alert of this.pendingCardAlerts()) {
      const existing = groups.get(alert.userId);
      if (existing) {
        existing.cards.push({ cardId: alert.cardId, last4: alert.last4, issuerName: alert.issuerName });
      } else {
        groups.set(alert.userId, {
          userId: alert.userId,
          userName: alert.userName,
          userEmail: alert.userEmail,
          cards: [{ cardId: alert.cardId, last4: alert.last4, issuerName: alert.issuerName }]
        });
      }
    }

    return Array.from(groups.values()).sort((a, b) => b.cards.length - a.cards.length || a.userName.localeCompare(b.userName));
  });

  adminLinks = [
    { path: '/admin/dashboard', label: 'Overview', icon: 'dashboard', description: 'System health & metrics' },
    { path: '/admin/users', label: 'User Management', icon: 'group', description: 'Monitor and control access' },
    { path: '/admin/issuers', label: 'Card Issuers', icon: 'account_balance', description: 'Manage fintech partners' },
    { path: '/admin/bills', label: 'Bill Generation', icon: 'receipt_long', description: 'Manual ledger control' },
    { path: '/admin/rewards', label: 'Reward Tiers', icon: 'military_tech', description: 'Configure cashback rates' },
    { path: '/admin/logs', label: 'System Logs', icon: 'database', description: 'Audit trail & templates' }
  ];

  ngOnInit() {
    this.loadPendingCardAlerts();
    this.refreshHandle = setInterval(() => this.loadPendingCardAlerts(), 60000);
    window.addEventListener('admin-alerts-refresh', this.refreshAlertsHandler);
  }

  ngOnDestroy() {
    if (this.refreshHandle) clearInterval(this.refreshHandle);
    this.alertsSub?.unsubscribe();
    window.removeEventListener('admin-alerts-refresh', this.refreshAlertsHandler);
  }

  toggleSidebar() {
    this.isSidebarOpen.update(v => !v);
  }

  closeSidebarOnMobile() {
    if (window.innerWidth < 1024) {
      this.isSidebarOpen.set(false);
    }
  }

  toggleAlerts() {
    this.showAlerts.update(v => !v);
    if (this.showAlerts()) {
      this.loadPendingCardAlerts();
    }
  }

  closeAlerts() {
    this.showAlerts.set(false);
  }

  openUsersForPending() {
    this.showAlerts.set(false);
    this.router.navigate(['/admin/users']);
  }

  openUserManagement(userId: string, cardId?: string) {
    this.showAlerts.set(false);
    const queryParams = cardId ? { userId, cardId } : { userId };
    this.router.navigate(['/admin/users'], { queryParams });
  }

  loadPendingCardAlerts() {
    this.isLoadingAlerts.set(true);
    this.alertsSub?.unsubscribe();

    this.alertsSub = this.adminService.getAllUsers({ page: 1, pageSize: 200 }).pipe(
      map((res: any) => (res?.data?.data?.users || res?.data?.users || res?.data || [])),
      catchError(() => of([])),
      map((users: any[]) => users.filter((u: any) => !!u?.id))
    ).subscribe((users: any[]) => {
      if (!users.length) {
        this.pendingCardAlerts.set([]);
        this.isLoadingAlerts.set(false);
        return;
      }

      const calls = users.map((u: any) =>
        this.adminService.getCardsByUser(u.id).pipe(
          map((res: any) => ({
            user: u,
            cards: (res?.data?.data || res?.data || []).filter((c: any) => (c?.creditLimit ?? 0) <= 0)
          })),
          catchError(() => of({ user: u, cards: [] }))
        )
      );

      forkJoin(calls).subscribe({
        next: (results: any[]) => {
          const alerts = results.flatMap((r: any) =>
            r.cards.map((c: any) => ({
              userId: r.user.id,
              userName: r.user.fullName || 'User',
              userEmail: r.user.email || '',
              cardId: c.id,
              last4: c.last4 || '----',
              issuerName: c.issuerName || 'Card'
            }))
          );

          this.pendingCardAlerts.set(alerts);
          this.isLoadingAlerts.set(false);
        },
        error: () => {
          this.pendingCardAlerts.set([]);
          this.isLoadingAlerts.set(false);
        }
      });
    });
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
