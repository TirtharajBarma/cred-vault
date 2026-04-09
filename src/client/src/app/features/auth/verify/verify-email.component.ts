import { Component, signal, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './verify-email.component.html'
})
export class VerifyEmailComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  verifyForm: FormGroup;
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  resendCountdown = signal(60);
  canResend = signal(false);
  private timer: any;

  constructor() {
    this.verifyForm = this.fb.group({
      otp: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6), Validators.pattern('^[0-9]*$')]]
    });
  }

  ngOnInit() {
    this.startResendTimer();
    if (!this.authService.pendingEmail()) {
      this.router.navigate(['/register']);
    }
  }

  ngOnDestroy() {
    this.stopTimer();
  }

  startResendTimer() {
    this.canResend.set(false);
    this.resendCountdown.set(60);
    if (this.timer) clearInterval(this.timer);
    
    this.timer = setInterval(() => {
      this.resendCountdown.update(v => v - 1);
      if (this.resendCountdown() <= 0) {
        this.canResend.set(true);
        this.stopTimer();
      }
    }, 1000);
  }

  stopTimer() {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  onResend() {
    const email = this.authService.pendingEmail();
    if (!email || !this.canResend()) return;

    this.authService.resendVerification({ email }).subscribe({
      next: (response) => {
        if (response.success) {
          this.successMessage.set('A new OTP has been sent to your email.');
          this.startResendTimer();
        } else {
          this.errorMessage.set(response.message || 'Failed to resend OTP.');
        }
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Connection error occurred.');
      }
    });
  }

  onSubmit() {
    const email = this.authService.pendingEmail();
    if (!email || this.verifyForm.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const { otp } = this.verifyForm.value;

    this.authService.verifyEmailOtp({ email, otp }).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (response.success) {
          this.router.navigate(['/dashboard']);
        } else {
          this.errorMessage.set(response.message || 'Invalid OTP. Please try again.');
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Connection error occurred.');
      }
    });
  }
}
