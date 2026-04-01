import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';

export interface Notification {
  id: string;
  subject: string;
  type: string;
  recipient: string;
  isSuccess: boolean;
  errorMessage: string | null;
  createdAtUtc: string;
}

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notifications.component.html',
  styleUrls: ['./notifications.component.css']
})
export class NotificationsComponent implements OnInit {
  private http = inject(HttpClient);
  private authService = inject(AuthService);

  notifications = signal<Notification[]>([]);
  isLoading = signal(true);
  filter = signal<'all' | 'success' | 'error'>('all');

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.isLoading.set(true);
    const email = this.authService.currentUser()?.email;
    if (!email) {
      this.isLoading.set(false);
      return;
    }

    this.http.get<any>(`http://localhost:5006/api/v1/notifications/logs?pageSize=100&email=${email}`).subscribe({
      next: (res) => {
        const data = res?.data?.data || res?.data || [];
        this.notifications.set(Array.isArray(data) ? data : []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  setFilter(f: string): void {
    this.filter.set(f as 'all' | 'success' | 'error');
  }

  getFilteredNotifications(): Notification[] {
    const f = this.filter();
    if (f === 'all') return this.notifications();
    if (f === 'success') return this.notifications().filter(n => n.isSuccess);
    return this.notifications().filter(n => !n.isSuccess);
  }

  getTypeIcon(type: string): string {
    switch (type?.toLowerCase()) {
      case 'paymentcompleted': return '💳';
      case 'paymentfailed': return '❌';
      case 'billgenerated': return '📄';
      case 'cardadded': return '💳';
      case 'userregistered': return '👤';
      case 'paymentotpgenerated': return '🔐';
      default: return '📧';
    }
  }

  getTypeColor(type: string): string {
    switch (type?.toLowerCase()) {
      case 'paymentcompleted': return 'bg-green-100 text-green-700';
      case 'paymentfailed': return 'bg-red-100 text-red-700';
      case 'billgenerated': return 'bg-blue-100 text-blue-700';
      case 'cardadded': return 'bg-purple-100 text-purple-700';
      default: return 'bg-slate-100 text-slate-700';
    }
  }
}
