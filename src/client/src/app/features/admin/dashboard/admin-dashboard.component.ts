import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './admin-dashboard.component.html'
})
export class AdminDashboardComponent implements OnInit {
  private adminService = inject(AdminService);
  private router = inject(Router);
  
  isLoading = signal(true);
  isRefreshing = signal(false);
  lastUpdated = signal<Date | null>(null);

  stats = signal({
    totalUsers: 0,
    activeUsers: 0,
    suspendedUsers: 0,
    pendingUsers: 0,
    activeIssuers: 0,
    rewardTiers: 0,
    systemUptime: '99.98%'
  });

  recentLogs = signal<any[]>([]);

  ngOnInit() {
    this.fetchDashboardData();
  }

  onAuditRefresh() {
    this.isRefreshing.set(true);
    this.fetchDashboardData();
  }

  fetchDashboardData() {
    this.isLoading.set(true);
    
    forkJoin({
      stats: this.adminService.getUserStats(),
      issuers: this.adminService.getIssuers(),
      tiers: this.adminService.getRewardTiers(),
      logs: this.adminService.getAuditLogs({ pageSize: 10 })
    } as any).subscribe({
      next: (res: any) => {
        const userStats = res.stats?.data?.data || res.stats?.data || {};
        const issuers = res.issuers?.data?.data || res.issuers?.data || [];
        const tiers = res.tiers?.data?.data || res.tiers?.data || [];
        const logs = res.logs?.data?.data?.logs || res.logs?.data?.data || res.logs?.data || [];

        this.stats.set({
          totalUsers: userStats.totalUsers || 0,
          activeUsers: userStats.activeUsers || 0,
          suspendedUsers: userStats.suspendedUsers || 0,
          pendingUsers: userStats.pendingUsers || 0,
          activeIssuers: issuers.filter((i: any) => i.isActive).length,
          rewardTiers: tiers.length,
          systemUptime: '99.98%'
        });

        this.recentLogs.set(logs);
        this.lastUpdated.set(new Date());
        this.isLoading.set(false);
        this.isRefreshing.set(false);
      },
      error: (err) => {
        console.error('Dashboard fetch error:', err);
        this.isLoading.set(false);
        this.isRefreshing.set(false);
      }
    });
  }

  onGrantAccess() {
    this.router.navigate(['/admin/users']);
  }

  onEmergencyBill() {
    this.router.navigate(['/admin/bills']);
  }

  onResyncAssets() {
    if (confirm('This will re-sync all card balances across all nodes. Continue?')) {
      this.isRefreshing.set(true);
      setTimeout(() => {
        this.fetchDashboardData();
      }, 2000);
    }
  }

  onBlastUpdate() {
    const message = prompt('Enter the broadcast message for all active users:');
    if (message && message.trim()) {
      alert(`Broadcast queued: "${message}"\n\nThis feature would send a notification to all active users.`);
    }
  }

  navigateToLogs() {
    this.router.navigate(['/admin/logs']);
  }

  formatTime(date: Date | null): string {
    if (!date) return '';
    return date.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Asia/Kolkata'
    });
  }

  getStatusColor(status: string): string {
    const colors: { [key: string]: string } = {
      'active': 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20',
      'suspended': 'bg-red-500/10 text-red-600 border-red-500/20',
      'pendingverification': 'bg-amber-500/10 text-amber-600 border-amber-500/20',
      'created': 'bg-blue-500/10 text-blue-600 border-blue-500/20',
      'deleted': 'bg-slate-500/10 text-slate-600 border-slate-500/20'
    };
    return colors[status] || 'bg-slate-500/10 text-slate-600 border-slate-500/20';
  }

  getActionIcon(action: string): string {
    const icons: { [key: string]: string } = {
      'Consumed': 'download',
      'Created': 'add_circle',
      'Updated': 'edit',
      'Deleted': 'delete',
      'Published': 'send'
    };
    return icons[action] || 'settings';
  }
}
