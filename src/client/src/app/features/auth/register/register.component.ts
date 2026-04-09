import { Component, signal, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css'
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  registerForm: FormGroup;
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  private readonly emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
  private readonly strongPasswordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/;

  constructor() {
    this.registerForm = this.fb.group({
      firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
      lastName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
      email: ['', [Validators.required, Validators.email, Validators.pattern(this.emailPattern)]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128), Validators.pattern(this.strongPasswordPattern)]],
      confirmPassword: ['', Validators.required]
    }, { validators: this.passwordMatchValidator });
  }

  get firstNameControl() {
    return this.registerForm.get('firstName');
  }

  get lastNameControl() {
    return this.registerForm.get('lastName');
  }

  get emailControl() {
    return this.registerForm.get('email');
  }

  get passwordControl() {
    return this.registerForm.get('password');
  }

  get confirmPasswordControl() {
    return this.registerForm.get('confirmPassword');
  }

  getNameError(field: 'firstName' | 'lastName'): string {
    const control = field === 'firstName' ? this.firstNameControl : this.lastNameControl;
    if (!control || !control.touched || !control.errors) return '';
    if (control.errors['required']) return 'This field is required';
    if (control.errors['minlength']) return 'Must be at least 2 characters';
    if (control.errors['maxlength']) return 'Must be under 50 characters';
    return 'Invalid value';
  }

  getEmailError(): string {
    const control = this.emailControl;
    if (!control || !control.touched || !control.errors) return '';
    if (control.errors['required']) return 'Email is required';
    return 'Enter a valid email address';
  }

  getPasswordError(): string {
    const control = this.passwordControl;
    if (!control || !control.touched || !control.errors) return '';
    if (control.errors['required']) return 'Password is required';
    if (control.errors['minlength']) return 'Password must be at least 8 characters';
    if (control.errors['maxlength']) return 'Password is too long';
    if (control.errors['pattern']) return 'Use uppercase, lowercase, number, and symbol';
    return 'Invalid password';
  }

  getConfirmPasswordError(): string {
    const control = this.confirmPasswordControl;
    if (!control || !control.touched) return '';
    if (control.errors?.['required']) return 'Please confirm your password';
    if (this.registerForm.errors?.['mismatch']) return 'Passwords do not match';
    return '';
  }

  passwordMatchValidator(g: FormGroup) {
    return g.get('password')?.value === g.get('confirmPassword')?.value
      ? null : { 'mismatch': true };
  }

  onSubmit() {
    this.registerForm.markAllAsTouched();
    if (this.registerForm.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const firstName = (this.firstNameControl?.value || '').trim();
    const lastName = (this.lastNameControl?.value || '').trim();
    const email = (this.emailControl?.value || '').trim().toLowerCase();
    const password = this.passwordControl?.value;

    this.firstNameControl?.setValue(firstName, { emitEvent: false });
    this.lastNameControl?.setValue(lastName, { emitEvent: false });
    this.emailControl?.setValue(email, { emitEvent: false });

    const fullName = `${firstName} ${lastName}`.trim();

    this.authService.register({ email, password, fullName }).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (response.success) {
          this.router.navigate(['/verify-email']);
        } else {
          this.errorMessage.set(response.message || 'Registration failed');
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Connection error occurred');
      }
    });
  }
}
