import { Injectable, signal } from '@angular/core';
import { interval, Subscription } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class RefreshService {
  private refreshSignal = signal(0);
  private timerSubscription: Subscription | null = null;
  private readonly intervalMs = 15000; // 15 seconds

  get signal() {
    return this.refreshSignal.asReadonly();
  }

  start(): void {
    if (this.timerSubscription) return;
    
    this.timerSubscription = interval(this.intervalMs).subscribe(() => {
      this.refreshSignal.update(v => v + 1);
    });
  }

  stop(): void {
    if (this.timerSubscription) {
      this.timerSubscription.unsubscribe();
      this.timerSubscription = null;
    }
  }
}
