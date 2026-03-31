import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-system-logs',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './system-logs.component.html'
})
export class SystemLogsComponent implements OnInit {
  private adminService = inject(AdminService);
  
  isLoading = signal(true);
  auditLogs = signal<any[]>([]);
  notificationLogs = signal<any[]>([]);
  activeTab = signal<'audit' | 'activity'>('audit');

  ngOnInit() {
    this.fetchData();
  }

  fetchData() {
    this.isLoading.set(true);
    
    this.adminService.getAuditLogs({ pageSize: 50 }).subscribe({
      next: (res: any) => {
        const logsData = res.data?.data?.logs || res.data?.data || res.data || [];
        this.auditLogs.set(logsData);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  fetchNotificationLogs() {
    this.isLoading.set(true);
    this.adminService.getNotificationLogs({ pageSize: 50 }).subscribe({
      next: (res: any) => {
        const logsData = res.data?.data?.logs || res.data?.data || res.data || [];
        this.notificationLogs.set(logsData);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  switchTab(tab: 'audit' | 'activity') {
    this.activeTab.set(tab);
    if (tab === 'activity') {
      this.fetchNotificationLogs();
    } else {
      this.fetchData();
    }
  }
}