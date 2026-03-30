import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.component.html'
})
export class ResetPasswordComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  resetForm: FormGroup;
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  constructor() {
    this.resetForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      otp: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]],
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', Validators.required]
    }, { validators: this.passwordMatchValidator });
  }

  ngOnInit() {
    // Prefill email if available from forgot-password step
    const email = this.authService.pendingEmail();
    if (email) {
      this.resetForm.get('email')?.setValue(email);
    }
  }

  passwordMatchValidator(g: FormGroup) {
    return g.get('newPassword')?.value === g.get('confirmPassword')?.value
      ? null : { 'mismatch': true };
  }

  onSubmit() {
    if (this.resetForm.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { email, otp, newPassword } = this.resetForm.value;

    this.authService.resetPassword({ email, otp, newPassword }).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (response.success) {
          console.log('[ResetPassword] Success! Navigating to login...');
          this.router.navigate(['/login']);
        } else {
          this.errorMessage.set(response.message || 'Failed to reset password');
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Connection error occurred');
      }
    });
  }
}
