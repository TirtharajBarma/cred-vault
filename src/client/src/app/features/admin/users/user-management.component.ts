import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-management.component.html'
})
export class UserManagementComponent implements OnInit, OnDestroy {
  private adminService = inject(AdminService);
  
  Math = Math;
  toastMessage = signal<string | null>(null);
  private toastHandle: ReturnType<typeof setTimeout> | null = null;

  // Search & List
  searchQuery = '';
  users = signal<any[]>([]);
  totalUsers = signal(0);
  currentPage = signal(1);
  pageSize = 10;
  isLoading = signal(false);
  statusFilter = signal<string>('all');

  // Selected User
  selectedUser = signal<any>(null);
  activeTab = signal<'cards' | 'bills' | 'statements' | 'logs' | 'audit'>('cards');

  // User Data
  selectedUserCards = signal<any[]>([]);
  userBills = signal<any[]>([]);
  userStatements = signal<any[]>([]);
  userLogs = signal<any[]>([]);
  userAudit = signal<any[]>([]);

  // Loading states
  isLoadingCards = signal(false);
  isLoadingBills = signal(false);
  isLoadingStatements = signal(false);
  isLoadingLogs = signal(false);
  isLoadingAudit = signal(false);

  // Card Modal
  selectedCard = signal<any>(null);
  editCreditLimit: number | null = null;
  editOutstandingBalance: number | null = null;
  editBillingCycleStartDay: number | null = null;
  isUpdatingCard = signal(false);
  cardUpdateMessage = signal<string | null>(null);

  // Messages
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  private panelMessageHandle: ReturnType<typeof setTimeout> | null = null;

  statusFilters = [
    { value: 'all', label: 'All' },
    { value: 'active', label: 'Active' },
    { value: 'pendingverification', label: 'Pending' },
    { value: 'suspended', label: 'Suspended' },
    { value: 'blocked', label: 'Blocked' }
  ];

  tabs = [
    { id: 'cards' as const, label: 'Cards', icon: '💳' },
    { id: 'bills' as const, label: 'Bills', icon: '📄' },
    { id: 'statements' as const, label: 'Statements', icon: '📊' },
    { id: 'logs' as const, label: 'Logs', icon: '📋' },
    { id: 'audit' as const, label: 'Audit', icon: '🔍' }
  ];

  ngOnInit() {
    this.loadUsers();
  }

  ngOnDestroy() {
    if (this.toastHandle) clearTimeout(this.toastHandle);
    if (this.panelMessageHandle) clearTimeout(this.panelMessageHandle);
  }

  loadUsers(page: number = 1) {
    this.isLoading.set(true);
    this.error.set(null);
    const params: any = { page, pageSize: this.pageSize };
    if (this.statusFilter() !== 'all') params.status = this.statusFilter();
    
    this.adminService.getAllUsers(params).subscribe({
      next: (res) => {
        const data = res.data?.data || res.data || {};
        this.users.set(data.users || []);
        this.totalUsers.set(data.total || 0);
        this.currentPage.set(data.page || 1);
        this.isLoading.set(false);
      },
      error: () => {
        this.showError('Failed to load users');
        this.isLoading.set(false);
      }
    });
  }

  searchUser() {
    const query = this.searchQuery.trim();
    if (!query) {
      this.loadUsers();
      return;
    }
    
    this.isLoading.set(true);
    const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(query);

    if (isGuid) {
      this.adminService.getUserDetails(query).subscribe({
        next: (res: any) => {
          const user = res?.data?.data?.user || res?.data?.user || res?.data;
          if (user?.id) {
            this.users.set([user]);
            this.totalUsers.set(1);
            this.selectUser(user);
          } else {
            this.users.set([]);
            this.totalUsers.set(0);
          }
          this.isLoading.set(false);
        },
        error: () => {
          this.users.set([]);
          this.totalUsers.set(0);
          this.isLoading.set(false);
        }
      });
    } else {
      this.adminService.getAllUsers({ search: query, page: 1, pageSize: 50 }).subscribe({
        next: (res) => {
          const data = res.data?.data || res.data || {};
          const users = data.users || [];
          this.users.set(users);
          this.totalUsers.set(data.total || 0);
          this.currentPage.set(1);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false)
      });
    }
  }

  selectUser(user: any) {
    this.selectedUser.set(user);
    this.activeTab.set('cards');
    this.loadUserData();
  }

  loadUserData() {
    const user = this.selectedUser();
    if (!user) return;
    this.loadCards();
  }

  switchTab(tab: 'cards' | 'bills' | 'statements' | 'logs' | 'audit') {
    this.activeTab.set(tab);
    const user = this.selectedUser();
    if (!user) return;

    switch (tab) {
      case 'cards': this.loadCards(); break;
      case 'bills': this.loadBills(); break;
      case 'statements': this.loadStatements(); break;
      case 'logs': this.loadLogs(); break;
      case 'audit': this.loadAudit(); break;
    }
  }

  loadCards() {
    const user = this.selectedUser();
    if (!user) return;
    this.isLoadingCards.set(true);
    this.adminService.getCardsByUser(user.id).subscribe({
      next: (res) => {
        this.selectedUserCards.set(res.data?.data || res.data || []);
        this.isLoadingCards.set(false);
      },
      error: () => {
        this.selectedUserCards.set([]);
        this.isLoadingCards.set(false);
      }
    });
  }

  loadBills() {
    const user = this.selectedUser();
    if (!user) return;
    this.isLoadingBills.set(true);
    this.adminService.getUserBills(user.id).subscribe({
      next: (res) => {
        this.userBills.set(res.data?.data || res.data || []);
        this.isLoadingBills.set(false);
      },
      error: () => {
        this.userBills.set([]);
        this.isLoadingBills.set(false);
      }
    });
  }

  loadStatements() {
    const user = this.selectedUser();
    if (!user) return;
    this.isLoadingStatements.set(true);
    this.adminService.getUserStatements(user.id).subscribe({
      next: (res) => {
        const data = res.data?.data || res.data || res;
        this.userStatements.set(Array.isArray(data) ? data : (data.statements || []));
        this.isLoadingStatements.set(false);
      },
      error: () => {
        this.userStatements.set([]);
        this.isLoadingStatements.set(false);
      }
    });
  }

  loadLogs() {
    const user = this.selectedUser();
    if (!user) return;
    this.isLoadingLogs.set(true);
    this.adminService.getUserNotificationLogs(user.email, 1, 200).subscribe({
      next: (res) => {
        const data = res.data?.data || res.data || res;
        this.userLogs.set(Array.isArray(data.logs) ? data.logs : (Array.isArray(data) ? data : []));
        this.isLoadingLogs.set(false);
      },
      error: () => {
        this.userLogs.set([]);
        this.isLoadingLogs.set(false);
      }
    });
  }

  loadAudit() {
    const user = this.selectedUser();
    if (!user) return;
    this.isLoadingAudit.set(true);
    this.adminService.getUserAuditLogs(user.id, 1, 200).subscribe({
      next: (res) => {
        const data = res.data?.data || res.data || res;
        this.userAudit.set(Array.isArray(data.logs) ? data.logs : (Array.isArray(data) ? data : []));
        this.isLoadingAudit.set(false);
      },
      error: () => {
        this.userAudit.set([]);
        this.isLoadingAudit.set(false);
      }
    });
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
  }

  updateSelectedCard() {
    const card = this.selectedCard();
    if (!card) return;

    const creditLimit = Number(this.editCreditLimit ?? 0);
    if (!Number.isFinite(creditLimit) || creditLimit <= 0) {
      this.cardUpdateMessage.set('Credit limit must be greater than 0.');
      return;
    }

    this.isUpdatingCard.set(true);
    this.adminService.updateCardByAdmin(card.id, {
      creditLimit,
      outstandingBalance: this.editOutstandingBalance,
      billingCycleStartDay: this.editBillingCycleStartDay
    }).subscribe({
      next: (res) => {
        this.isUpdatingCard.set(false);
        if (res.success) {
          const updatedCard = res.data;
          this.selectedCard.set(updatedCard);
          this.selectedUserCards.update(cards => cards.map(c => c.id === updatedCard.id ? updatedCard : c));
          this.showToast('Card updated successfully');
          this.closeCardDetails();
        } else {
          this.cardUpdateMessage.set(res.message || 'Failed to update');
        }
      },
      error: () => {
        this.isUpdatingCard.set(false);
        this.cardUpdateMessage.set('Failed to update card');
      }
    });
  }

  updateStatus(status: string) {
    if (!this.selectedUser()) return;
    this.adminService.updateUserStatus(this.selectedUser().id, status).subscribe({
      next: (res) => {
        if (res.success) {
          this.selectedUser.update(u => ({ ...u, status }));
          this.loadUsers();
        }
      }
    });
  }

  updateRole(role: string) {
    if (!this.selectedUser()) return;
    this.adminService.updateUserRole(this.selectedUser().id, role).subscribe({
      next: (res) => {
        if (res.success) {
          this.selectedUser.update(u => ({ ...u, role }));
          this.loadUsers();
        }
      }
    });
  }

  nextPage() {
    if (this.currentPage() < this.getTotalPages()) {
      this.loadUsers(this.currentPage() + 1);
    }
  }

  prevPage() {
    if (this.currentPage() > 1) this.loadUsers(this.currentPage() - 1);
  }

  goToPage(page: number) {
    if (page >= 1 && page <= this.getTotalPages() && page !== this.currentPage()) {
      this.loadUsers(page);
    }
  }

  getTotalPages(): number {
    return Math.max(1, Math.ceil(this.totalUsers() / this.pageSize));
  }

  getVisiblePages(): number[] {
    const totalPages = this.getTotalPages();
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

  canUpdateStatus(status: string): boolean {
    const user = this.selectedUser();
    if (!user?.status) return false;
    return user.status.toLowerCase() !== status.toLowerCase();
  }

  canPromoteToAdmin(): boolean {
    const user = this.selectedUser();
    return !!user && user.role !== 'admin';
  }

  private showToast(message: string) {
    this.toastMessage.set(message);
    if (this.toastHandle) clearTimeout(this.toastHandle);
    this.toastHandle = setTimeout(() => this.toastMessage.set(null), 2000);
  }

  private showSuccess(message: string) {
    this.successMessage.set(message);
    if (this.panelMessageHandle) clearTimeout(this.panelMessageHandle);
    this.panelMessageHandle = setTimeout(() => this.successMessage.set(null), 2500);
  }

  private showError(message: string) {
    this.error.set(message);
    if (this.panelMessageHandle) clearTimeout(this.panelMessageHandle);
    this.panelMessageHandle = setTimeout(() => this.error.set(null), 3000);
  }

  getStatusLabel(status: string): string {
    const labels: { [key: string]: string } = {
      'active': 'Active', 'suspended': 'Suspended',
      'pendingverification': 'Pending', 'deleted': 'Deleted', 'blocked': 'Blocked'
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

  getBillStatusColor(status: number): string {
    const colors: { [key: number]: string } = {
      0: 'bg-amber-500/10 text-amber-600 border-amber-500/20',
      1: 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20',
      2: 'bg-blue-500/10 text-blue-600 border-blue-500/20',
      3: 'bg-purple-500/10 text-purple-600 border-purple-500/20',
      4: 'bg-red-500/10 text-red-600 border-red-500/20',
      5: 'bg-teal-500/10 text-teal-600 border-teal-500/20'
    };
    return colors[status] || 'bg-slate-500/10 text-slate-600 border-slate-500/20';
  }

  getBillStatusLabel(status: number): string {
    const labels: { [key: number]: string } = {
      0: 'Pending', 1: 'Paid', 2: 'Overdue', 3: 'PartiallyPaid', 4: 'Cancelled', 5: 'PartiallyPaid'
    };
    return labels[status] || 'Unknown';
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
  }
}
