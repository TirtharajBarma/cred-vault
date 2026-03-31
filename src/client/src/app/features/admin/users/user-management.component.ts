import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-management.component.html'
})
export class UserManagementComponent {
  private adminService = inject(AdminService);
  Math = Math;
  
  activeTab = signal<'search' | 'list'>('list');
  searchQuery = '';
  isLoading = signal(false);
  selectedUser = signal<any>(null);
  error = signal<string | null>(null);

  users = signal<any[]>([]);
  totalUsers = signal(0);
  currentPage = signal(1);
  pageSize = 20;

  constructor() {
    this.loadUsers();
  }

  switchTab(tab: 'search' | 'list') {
    this.activeTab.set(tab);
    if (tab === 'list') {
      this.loadUsers();
    }
  }

  loadUsers(page: number = 1) {
    this.isLoading.set(true);
    this.adminService.getAllUsers({ page, pageSize: this.pageSize }).subscribe({
      next: (res) => {
        // API returns nested: res.data.data
        const data = res.data?.data || res.data || {};
        this.users.set(data.users || []);
        this.totalUsers.set(data.total || 0);
        this.currentPage.set(data.page || 1);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Load users error:', err);
        this.isLoading.set(false);
      }
    });
  }

  nextPage() {
    const totalPages = Math.ceil(this.totalUsers() / this.pageSize);
    if (this.currentPage() < totalPages) {
      this.loadUsers(this.currentPage() + 1);
    }
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.loadUsers(this.currentPage() - 1);
    }
  }
  
  searchUser() {
    if (!this.searchQuery) return;
    
    this.isLoading.set(true);
    this.error.set(null);
    this.selectedUser.set(null);

    // Simple email regex check
    const isEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.searchQuery);

    if (isEmail) {
      // Search by email using getAllUsers with search param
      this.adminService.getAllUsers({ search: this.searchQuery }).subscribe({
        next: (res) => {
          const data = res.data?.data || res.data || {};
          const users = data.users || [];
          if (users.length > 0) {
            this.selectedUser.set(users[0]);
          } else {
            this.error.set('No user found with this email address.');
          }
          this.isLoading.set(false);
        },
        error: (err) => {
          console.error('Email search error:', err);
          this.error.set('Search failed. Please try again.');
          this.isLoading.set(false);
        }
      });
    } else {
      // Try searching by ID
      this.adminService.getUserDetails(this.searchQuery).subscribe({
        next: (res) => {
          this.selectedUser.set(res.data);
          this.isLoading.set(false);
        },
        error: (err) => {
          console.error('ID search error:', err);
          this.error.set('User not found by ID. Try searching by email.');
          this.isLoading.set(false);
        }
      });
    }
  }

  updateStatus(status: number) {
    if (!this.selectedUser()) return;
    
    const statusMap: { [key: number]: string } = {
      1: 'pendingverification',
      2: 'active',
      3: 'suspended'
    };
    
    this.adminService.updateUserStatus(this.selectedUser().id, status).subscribe({
      next: (res) => {
        if (res.success) {
          this.selectedUser.update(u => ({ ...u, status: statusMap[status] }));
        }
      }
    });
  }

  getStatusName(status: number): string {
    switch (status) {
      case 1: return 'Pending Verification';
      case 2: return 'Active';
      case 3: return 'Suspended';
      default: return 'Unknown';
    }
  }
}
