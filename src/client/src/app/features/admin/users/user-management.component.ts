import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-management.component.html'
})
export class UserManagementComponent implements OnInit, OnDestroy {
  private adminService = inject(AdminService);
  private route = inject(ActivatedRoute);
  Math = Math;
  
  activeTab = signal<'search' | 'list'>('list');
  searchQuery = '';
  isLoading = signal(false);
  selectedUser = signal<any>(null);
  selectedUserCards = signal<any[]>([]);
  isLoadingCards = signal(false);
  selectedCard = signal<any>(null);
  isUpdatingCard = signal(false);
  cardUpdateMessage = signal<string | null>(null);
  toastMessage = signal<string | null>(null);
  private panelMessageHandle: ReturnType<typeof setTimeout> | null = null;
  private toastHandle: ReturnType<typeof setTimeout> | null = null;
  pendingOpenCardId: string | null = null;
  editCreditLimit: number | null = null;
  editOutstandingBalance: number | null = null;
  editBillingCycleStartDay: number | null = null;
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  users = signal<any[]>([]);
  totalUsers = signal(0);
  currentPage = signal(1);
  pageSize = 10;

  statusFilter = signal<string>('all');
  cardListMode = signal<'pending' | 'all'>('pending');

  statusFilters = [
    { value: 'all', label: 'All', activeClass: 'bg-charcoal text-white' },
    { value: 'active', label: 'Active', activeClass: 'bg-emerald-500 text-white' },
    { value: 'pendingverification', label: 'Pending', activeClass: 'bg-amber-500 text-white' },
    { value: 'suspended', label: 'Suspended', activeClass: 'bg-red-500 text-white' },
    { value: 'blocked', label: 'Blocked', activeClass: 'bg-red-700 text-white' }
  ];

  getCountForFilter(filter: string): number {
    const counts = this.getAllStatusCounts();
    if (filter === 'all') return counts.total;
    return counts[filter as keyof typeof counts] || 0;
  }

  filteredUsers = computed(() => {
    const filter = this.statusFilter();
    const allUsers = this.users();
    if (filter === 'all') return allUsers;
    return allUsers.filter(u => u.status?.toLowerCase() === filter.toLowerCase());
  });

  visibleCards = computed(() => {
    const cards = [...this.selectedUserCards()];
    if (this.cardListMode() === 'all') return cards;
    return cards.sort((a, b) => Number((b.creditLimit ?? 0) <= 0) - Number((a.creditLimit ?? 0) <= 0));
  });

  constructor() {
    this.loadUsers();
  }

  ngOnInit() {
    this.route.queryParamMap.subscribe(params => {
      const userId = params.get('userId');
      this.pendingOpenCardId = params.get('cardId');
      if (userId) {
        this.openUserFromAlert(userId);
        return;
      }
    });
  }

  ngOnDestroy() {
    if (this.toastHandle) {
      clearTimeout(this.toastHandle);
    }
    if (this.panelMessageHandle) {
      clearTimeout(this.panelMessageHandle);
    }
  }

  private openUserFromAlert(userId: string) {
    this.isLoading.set(true);
    this.error.set(null);
    this.successMessage.set(null);

    this.adminService.getUserDetails(userId).subscribe({
      next: (res: any) => {
        const user = res?.data?.data?.user || res?.data?.user || res?.data;
        if (!user?.id) {
          this.showError('User not found for selected alert.');
          this.isLoading.set(false);
          return;
        }

        this.users.set([user]);
        this.totalUsers.set(1);
        this.currentPage.set(1);
        this.selectUser(user);
        this.showSuccess('Alert user opened.');
        this.isLoading.set(false);
      },
      error: () => {
        this.showError('Failed to open alert user.');
        this.isLoading.set(false);
      }
    });
  }

  switchTab(tab: 'search' | 'list') {
    this.activeTab.set(tab);
    this.selectedUser.set(null);
    this.selectedUserCards.set([]);
    if (tab === 'list') {
      this.loadUsers();
    }
  }

  setStatusFilter(status: string) {
    this.statusFilter.set(status);
  }

  loadUsers(page: number = 1) {
    this.isLoading.set(true);
    this.error.set(null);
    
    const params: any = { page, pageSize: this.pageSize };
    if (this.statusFilter() !== 'all') {
      params.status = this.statusFilter();
    }
    
    this.adminService.getAllUsers(params).subscribe({
      next: (res) => {
        const data = res.data?.data || res.data || {};
        this.users.set(data.users || []);
        this.totalUsers.set(data.total || 0);
        this.currentPage.set(data.page || 1);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Load users error:', err);
        this.showError('Failed to load users');
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
  
  selectUser(user: any) {
    this.selectedUser.set(user);
    this.selectedUserCards.set([]);
    this.isLoadingCards.set(true);
    this.error.set(null);
    this.successMessage.set(null);
    this.cardListMode.set('pending');

    this.adminService.getCardsByUser(user.id).subscribe({
      next: (res) => {
        const cards = res.data?.data || res.data || [];
        this.selectedUserCards.set(cards);

        if (this.pendingOpenCardId) {
          const targetCard = cards.find((c: any) => c.id === this.pendingOpenCardId);
          if (targetCard) {
            this.showCardDetails(targetCard);
          }
          this.pendingOpenCardId = null;
        }

        this.isLoadingCards.set(false);
      },
      error: () => {
        this.selectedUserCards.set([]);
        this.isLoadingCards.set(false);
      }
    });
  }
  
  searchUser() {
    const query = this.searchQuery.trim();
    if (!query) return;
    
    this.isLoading.set(true);
    this.error.set(null);
    this.selectedUser.set(null);
    this.selectedUserCards.set([]);
    this.successMessage.set(null);

    const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(query);

    if (isGuid) {
      this.adminService.getUserDetails(query).subscribe({
        next: (res: any) => {
          const user = res?.data?.data?.user || res?.data?.user || res?.data;
          if (user?.id) {
            this.users.set([user]);
            this.totalUsers.set(1);
            this.currentPage.set(1);
            this.selectUser(user);
            this.showSuccess('User found by ID.');
          } else {
            this.showError('User not found by ID.');
          }
          this.isLoading.set(false);
        },
        error: () => {
          this.showError('User not found by ID.');
          this.isLoading.set(false);
        }
      });
      return;
    }

    this.adminService.getAllUsers({ search: query, page: 1, pageSize: 50 }).subscribe({
      next: (res) => {
        const data = res.data?.data || res.data || {};
        const users = data.users || [];

        this.users.set(users);
        this.totalUsers.set(users.length);
        this.currentPage.set(1);

        if (users.length > 0) {
          this.selectUser(users[0]);
          this.showSuccess(`Found ${users.length} user${users.length === 1 ? '' : 's'}.`);
        } else {
          this.showError('No user found for this search value.');
        }

        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('User search error:', err);
        this.showError('Search failed. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  updateStatus(status: string) {
    if (!this.selectedUser()) return;
    
    this.error.set(null);
    this.successMessage.set(null);
    
    this.adminService.updateUserStatus(this.selectedUser().id, status).subscribe({
      next: (res) => {
        if (res.success) {
          this.selectedUser.update(u => ({ ...u, status: status }));
          this.loadUsers();
          this.showSuccess(`User status updated to ${this.getStatusLabel(status)}`);
        } else {
          this.showError(res.message || 'Failed to update status');
        }
      },
      error: (err) => {
        this.showError('Failed to update user status');
      }
    });
  }

  updateRole(role: string) {
    if (!this.selectedUser()) return;
    
    this.error.set(null);
    this.successMessage.set(null);
    
    this.adminService.updateUserRole(this.selectedUser().id, role).subscribe({
      next: (res) => {
        if (res.success) {
          this.selectedUser.update(u => ({ ...u, role: role }));
          this.loadUsers();
          this.showSuccess(`User role updated to ${role}`);
        } else {
          this.showError(res.message || 'Failed to update role');
        }
      },
      error: (err) => {
        this.showError('Failed to update user role');
      }
    });
  }

  toggleRole() {
    if (!this.selectedUser()) return;
    const currentRole = this.selectedUser().role?.toLowerCase();
    const newRole = currentRole === 'admin' ? 'user' : 'admin';
    this.updateRole(newRole);
  }

  showCardDetails(card: any) {
    this.selectedCard.set(card);
    this.cardUpdateMessage.set(null);
    this.editCreditLimit = Number(card.creditLimit ?? 0);
    this.editOutstandingBalance = Number(card.outstandingBalance ?? 0);
    this.editBillingCycleStartDay = Number(card.billingCycleStartDay ?? 1);
  }

  closeCardDetails() {
    this.selectedCard.set(null);
    this.cardUpdateMessage.set(null);
  }

  updateSelectedCard() {
    const card = this.selectedCard();
    if (!card) return;

    const creditLimit = Number(this.editCreditLimit ?? 0);
    const outstandingBalance = this.editOutstandingBalance === null ? null : Number(this.editOutstandingBalance);
    const billingCycleStartDay = this.editBillingCycleStartDay === null ? null : Number(this.editBillingCycleStartDay);

    if (!Number.isFinite(creditLimit) || creditLimit <= 0) {
      this.cardUpdateMessage.set('Credit limit must be greater than 0.');
      return;
    }

    if (outstandingBalance !== null && (!Number.isFinite(outstandingBalance) || outstandingBalance < 0)) {
      this.cardUpdateMessage.set('Outstanding balance cannot be negative.');
      return;
    }

    if (billingCycleStartDay !== null && (!Number.isFinite(billingCycleStartDay) || billingCycleStartDay < 1 || billingCycleStartDay > 31)) {
      this.cardUpdateMessage.set('Billing cycle day must be between 1 and 31.');
      return;
    }

    this.isUpdatingCard.set(true);
    this.cardUpdateMessage.set(null);

    this.adminService.updateCardByAdmin(card.id, {
      creditLimit,
      outstandingBalance,
      billingCycleStartDay
    }).subscribe({
      next: (res) => {
        this.isUpdatingCard.set(false);
        if (!res.success || !res.data) {
          this.cardUpdateMessage.set(res.message || 'Failed to update card.');
          return;
        }

        const updatedCard = res.data;
        this.selectedCard.set(updatedCard);
        this.selectedUserCards.update(cards => cards.map(c => c.id === updatedCard.id ? updatedCard : c));
        window.dispatchEvent(new Event('admin-alerts-refresh'));
        this.showSuccess('Card details updated successfully.');
        this.showToast('Card details updated.');
        this.closeCardDetails();
      },
      error: () => {
        this.isUpdatingCard.set(false);
        this.cardUpdateMessage.set('Failed to update card.');
      }
    });
  }

  setCardListMode(mode: 'pending' | 'all') {
    this.cardListMode.set(mode);
  }

  private showToast(message: string) {
    this.toastMessage.set(message);
    if (this.toastHandle) {
      clearTimeout(this.toastHandle);
    }
    this.toastHandle = setTimeout(() => this.toastMessage.set(null), 2000);
  }

  private showSuccess(message: string) {
    this.successMessage.set(message);
    this.error.set(null);
    if (this.panelMessageHandle) {
      clearTimeout(this.panelMessageHandle);
    }
    this.panelMessageHandle = setTimeout(() => this.successMessage.set(null), 2500);
  }

  private showError(message: string) {
    this.error.set(message);
    this.successMessage.set(null);
    if (this.panelMessageHandle) {
      clearTimeout(this.panelMessageHandle);
    }
    this.panelMessageHandle = setTimeout(() => this.error.set(null), 3000);
  }

  getStatusLabel(status: string): string {
    const labels: { [key: string]: string } = {
      'active': 'Active',
      'suspended': 'Suspended',
      'pendingverification': 'Pending Verification',
      'deleted': 'Deleted',
      'blocked': 'Blocked'
    };
    return labels[status?.toLowerCase()] || status || 'Unknown';
  }

  getStatusColor(status: string): string {
    const colors: { [key: string]: string } = {
      'active': 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20',
      'suspended': 'bg-red-500/10 text-red-600 border-red-500/20',
      'pendingverification': 'bg-amber-500/10 text-amber-600 border-amber-500/20',
      'deleted': 'bg-slate-500/10 text-slate-600 border-slate-500/20',
      'blocked': 'bg-red-500/10 text-red-700 border-red-500/20'
    };
    return colors[status?.toLowerCase()] || 'bg-slate-500/10 text-slate-600 border-slate-500/20';
  }

  getStatusFilterCount(status: string): number {
    const allUsers = this.users();
    if (status === 'all') return allUsers.length;
    return allUsers.filter(u => u.status?.toLowerCase() === status.toLowerCase()).length;
  }

  getAllStatusCounts(): { active: number; suspended: number; pendingverification: number; blocked: number; total: number } {
    const allUsers = this.users();
    return {
      active: allUsers.filter(u => u.status?.toLowerCase() === 'active').length,
      suspended: allUsers.filter(u => u.status?.toLowerCase() === 'suspended').length,
      pendingverification: allUsers.filter(u => u.status?.toLowerCase() === 'pendingverification').length,
      blocked: allUsers.filter(u => u.status?.toLowerCase() === 'blocked').length,
      total: allUsers.length
    };
  }
}
