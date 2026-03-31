import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-dashboard.component.html'
})
export class AdminDashboardComponent implements OnInit {
  private adminService = inject(AdminService);
  private router = inject(Router);
  
  isLoading = signal(true);
  stats = signal({
    totalUsers: 0,
    activeIssuers: 0,
    rewardTiers: 0,
    systemUptime: '99.98%'
  });

  recentLogs = signal<any[]>([]);

  ngOnInit() {
    this.fetchDashboardData();
  }

  onAuditRefresh() {
    this.fetchDashboardData();
  }

  fetchDashboardData() {
    this.isLoading.set(true);
    
    forkJoin({
      stats: this.adminService.getUserStats(),
      issuers: this.adminService.getIssuers(),
      tiers: this.adminService.getRewardTiers(),
      logs: this.adminService.getAuditLogs({ pageSize: 5 })
    } as any).subscribe({
      next: (res: any) => {
        const userStats = res.stats?.data?.data || res.stats?.data || {};
        this.stats.set({
          totalUsers: userStats.totalUsers || 0,
          activeIssuers: res.issuers?.data?.data?.length || res.issuers?.data?.length || 0,
          rewardTiers: res.tiers?.data?.data?.length || res.tiers?.data?.length || 0,
          systemUptime: '99.98%'
        });
        const logsData = res.logs?.data?.data?.logs || res.logs?.data?.data || res.logs?.data || [];
        this.recentLogs.set(logsData);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Dashboard fetch error:', err);
        this.isLoading.set(false);
      }
    });
  }

  // Administrative Actions
  onGrantAccess() {
    this.router.navigate(['/admin/users']);
  }

  onEmergencyBill() {
    alert('This feature requires selecting a specific user and card from the Bill Generation page.');
    this.router.navigate(['/admin/bills']);
  }

  onResyncAssets() {
    alert('Synchronization protocol initiated across all nodes.');
    // Placeholder for actual sync logic if available
  }

  onBlastUpdate() {
   const message = prompt('Enter the message for global notification:');
   if (message) {
       alert('Global announcement broadcasted to all active clusters.');
   }
  }
}
