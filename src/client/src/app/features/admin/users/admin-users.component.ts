import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <div class="mb-6">
        <h1 class="text-2xl font-bold text-gray-900">User Management</h1>
        <p class="text-gray-500 text-sm">Search and manage all users</p>
      </div>

      <!-- Search -->
      <div class="bg-white border border-gray-200 rounded-xl p-4 mb-6">
        <div class="flex flex-col md:flex-row gap-3">
          <input [(ngModel)]="searchQuery" (keyup.enter)="search()" type="text"
                 placeholder="Search by email, name, or paste user ID"
                 class="flex-1 px-4 py-2.5 border border-gray-300 rounded-lg" />
          <button (click)="search()" class="px-6 py-2.5 bg-gray-800 text-white rounded-lg font-medium hover:bg-gray-700">
            Search
          </button>
          <button (click)="clearSearch()" class="px-4 py-2.5 bg-white border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-100">
            Clear
          </button>
        </div>
      </div>

      <!-- User List -->
      <div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
        <div class="px-4 py-3 bg-gray-50 border-b border-gray-200 flex justify-between items-center">
          <span class="font-semibold text-gray-700">Users ({{ totalUsers() }})</span>
          @if (isLoading()) {
            <span class="text-sm text-gray-500">Loading...</span>
          }
        </div>

        @if (isLoading()) {
          <div class="p-8 text-center">
            <div class="animate-spin w-8 h-8 border-4 border-gray-300 border-t-gray-800 rounded-full mx-auto"></div>
          </div>
        } @else if (users().length > 0) {
          <div class="divide-y divide-gray-100">
            @for (user of users(); track user.id) {
              <div (click)="viewUser(user)" 
                   class="flex items-center gap-3 px-4 py-4 hover:bg-blue-50 cursor-pointer transition-all">
                <div class="w-10 h-10 shrink-0 bg-gray-800 rounded-xl flex items-center justify-center text-white font-bold text-base">
                  {{ user.fullName?.charAt(0) || '?' }}
                </div>
                <div class="flex-1 min-w-0">
                  <p class="font-semibold text-gray-900 truncate">{{ user.fullName }}</p>
                  <p class="text-sm text-gray-500 truncate">{{ user.email }}</p>
                  <div class="flex items-center gap-2 mt-1 flex-wrap">
                    <span class="px-2 py-0.5 rounded-full text-xs font-semibold border {{ getStatusClass(user.status) }}">
                      {{ getStatusLabel(user.status) }}
                    </span>
                    <span class="px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-600">
                      {{ user.role }}
                    </span>
                  </div>
                </div>
                <span class="text-gray-400 shrink-0">→</span>
              </div>
            }
          </div>

          <!-- Pagination -->
          <div class="flex items-center justify-between px-6 py-4 bg-gray-50 border-t border-gray-200">
            <span class="text-sm text-gray-500">Page {{ currentPage() }} of {{ totalPagesCount }}</span>
            <div class="flex items-center gap-2">
              <button (click)="prevPage()" [disabled]="currentPage() === 1" 
                      class="px-4 py-2 bg-white border border-gray-300 rounded-lg text-sm hover:bg-gray-100 disabled:opacity-50">
                Previous
              </button>
              <div class="hidden sm:flex items-center gap-1">
                @for (page of visiblePages(); track page) {
                  <button (click)="goToPage(page)"
                          [disabled]="page === currentPage()"
                          [class]="page === currentPage() ? 'bg-gray-800 text-white border-gray-800 cursor-not-allowed' : 'bg-white text-gray-700 border-gray-300 hover:bg-gray-100'"
                          class="w-8 h-8 text-xs border rounded transition-all">
                    {{ page }}
                  </button>
                }
              </div>
              <button (click)="nextPage()" [disabled]="currentPage() >= totalPagesCount" 
                      class="px-4 py-2 bg-gray-800 text-white rounded-lg text-sm hover:bg-gray-700 disabled:opacity-50">
                Next
              </button>
            </div>
          </div>
        } @else {
          <div class="text-center py-16 text-gray-500">
            <p class="text-4xl mb-3">🔍</p>
            <p class="font-semibold">No users found</p>
            <p class="text-sm text-gray-400 mt-1">Try a different search term</p>
          </div>
        }
      </div>
    </div>
  `
})
export class AdminUsersComponent {
  private adminService = inject(AdminService);
  private router = inject(Router);

  searchQuery = '';
  users = signal<any[]>([]);
  totalUsers = signal(0);
  currentPage = signal(1);
  pageSize = 7;
  isLoading = signal(false);

  get totalPagesCount(): number {
    return Math.ceil(this.totalUsers() / this.pageSize) || 1;
  }

  constructor() {
    this.loadUsers();
  }

  loadUsers(page = 1) {
    this.isLoading.set(true);
    this.adminService.getAllUsers({ page, pageSize: this.pageSize }).subscribe({
      next: (res) => {
        const data = res.data?.data || res.data || {};
        this.users.set(data.users || []);
        this.totalUsers.set(data.total || 0);
        this.currentPage.set(page);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  search() {
    const q = this.searchQuery.trim();
    if (!q) {
      this.loadUsers();
      return;
    }

    this.isLoading.set(true);
    const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(q);

    if (isGuid) {
      this.adminService.getUserDetails(q).subscribe({
        next: (res: any) => {
          const user = res?.data?.data?.user || res?.data?.user || res?.data;
          this.users.set(user?.id ? [user] : []);
          this.totalUsers.set(user?.id ? 1 : 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.users.set([]);
          this.totalUsers.set(0);
          this.isLoading.set(false);
        }
      });
    } else {
      this.adminService.getAllUsers({ search: q, page: 1, pageSize: 50 }).subscribe({
        next: (res) => {
          const data = res.data?.data || res.data || {};
          this.users.set(data.users || []);
          this.totalUsers.set(data.total || 0);
          this.currentPage.set(1);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false)
      });
    }
  }

  clearSearch() {
    this.searchQuery = '';
    this.loadUsers();
  }

  viewUser(user: any) {
    this.router.navigate(['/admin/users', user.id]);
  }

  prevPage() {
    if (this.currentPage() > 1) this.loadUsers(this.currentPage() - 1);
  }

  goToPage(page: number) {
    if (page >= 1 && page <= this.totalPagesCount && page !== this.currentPage()) {
      this.loadUsers(page);
    }
  }

  visiblePages(): number[] {
    const totalPages = this.totalPagesCount;
    const current = this.currentPage();
    const maxVisible = 7;

    if (totalPages <= maxVisible) {
      return Array.from({ length: totalPages }, (_, i) => i + 1);
    }

    const half = Math.floor(maxVisible / 2);
    let start = Math.max(1, current - half);
    let end = start + maxVisible - 1;

    if (end > totalPages) {
      end = totalPages;
      start = end - maxVisible + 1;
    }

    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  }

  nextPage() {
    if (              this.currentPage() < this.totalPagesCount) this.loadUsers(this.currentPage() + 1);
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      'active': 'Active', 'suspended': 'Suspended',
      'pendingverification': 'Pending', 'blocked': 'Blocked'
    };
    return labels[status?.toLowerCase()] || status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      'active': 'bg-emerald-100 text-emerald-700 border-emerald-200',
      'suspended': 'bg-red-100 text-red-700 border-red-200',
      'pendingverification': 'bg-amber-100 text-amber-700 border-amber-200',
      'blocked': 'bg-red-200 text-red-800 border-red-300'
    };
    return classes[status?.toLowerCase()] || 'bg-gray-100 text-gray-700 border-gray-200';
  }
}
