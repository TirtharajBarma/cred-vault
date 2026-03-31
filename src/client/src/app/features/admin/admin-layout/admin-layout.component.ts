import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.css'
})
export class AdminLayoutComponent {
  private authService = inject(AuthService);
  private router = inject(Router);
  
  user = this.authService.currentUser;
  isSidebarOpen = signal(true);

  adminLinks = [
    { path: '/admin/dashboard', label: 'Overview', icon: 'dashboard', description: 'System health & metrics' },
    { path: '/admin/users', label: 'User Management', icon: 'group', description: 'Monitor and control access' },
    { path: '/admin/issuers', label: 'Card Issuers', icon: 'account_balance', description: 'Manage fintech partners' },
    { path: '/admin/bills', label: 'Bill Generation', icon: 'receipt_long', description: 'Manual ledger control' },
    { path: '/admin/rewards', label: 'Reward Tiers', icon: 'military_tech', description: 'Configure cashback rates' },
    { path: '/admin/logs', label: 'System Logs', icon: 'database', description: 'Audit trail & templates' }
  ];

  toggleSidebar() {
    this.isSidebarOpen.update(v => !v);
  }

  closeSidebarOnMobile() {
    if (window.innerWidth < 1024) {
      this.isSidebarOpen.set(false);
    }
  }

  logout() {
    this.authService.logout();
  }
}
