import { Component, signal, inject, AfterViewInit, ElementRef, ViewChild, NgZone } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { UserRole } from '../../../core/models/auth.models';
import { CommonModule } from '@angular/common';

// Google Identity Services types
declare const google: {
  accounts: {
    id: {
      initialize(config: {
        client_id: string;
        callback: (resp: { credential: string }) => void;
        auto_select?: boolean;
        cancel_on_tap_outside?: boolean;
      }): void;
      renderButton(parent: HTMLElement, options: {
        type?: string;
        theme?: string;
        size?: string;
        width?: number;
        text?: string;
      }): void;
    };
  };
};

const GOOGLE_CLIENT_ID = '450643445715-v7ae2hgkm4mi76j9pualqv7cecnru1c9.apps.googleusercontent.com';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent implements AfterViewInit {
  @ViewChild('googleBtnContainer') googleBtnContainer!: ElementRef<HTMLDivElement>;

  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private zone = inject(NgZone);

  loginForm: FormGroup;
  isLoading = signal(false);
  isGoogleLoading = signal(false);
  errorMessage = signal<string | null>(null);
  showPassword = signal(false);
  private readonly emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;

  constructor() {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email, Validators.pattern(this.emailPattern)]],
      password: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(128)]]
    });
  }

  ngAfterViewInit(): void {
    this.initGoogleButton();
  }

  private initGoogleButton(): void {
    // Wait for the GSI library to be ready (it's loaded async in index.html)
    const tryInit = () => {
      if (typeof google !== 'undefined' && google.accounts?.id) {
        google.accounts.id.initialize({
          client_id: GOOGLE_CLIENT_ID,
          callback: (response) => {
            // Run inside Angular's zone so signals/change detection work
            this.zone.run(() => this.handleGoogleCredential(response.credential));
          },
          auto_select: false,
          cancel_on_tap_outside: true
        });

        // Render Google's real button inside our container div.
        // It's invisible (opacity:0, pointer-events:none on wrapper) but
        // its iframe captures clicks forwarded from our styled button.
        google.accounts.id.renderButton(this.googleBtnContainer.nativeElement, {
          type: 'standard',
          theme: 'outline',
          size: 'large',
          width: this.googleBtnContainer.nativeElement.offsetWidth || 200
        });
      } else {
        // Retry until the async script is ready (usually < 500ms)
        setTimeout(tryInit, 100);
      }
    };
    tryInit();
  }

  private handleGoogleCredential(idToken: string): void {
    this.isGoogleLoading.set(true);
    this.errorMessage.set(null);

    this.authService.loginWithGoogle(idToken).subscribe({
      next: (response) => {
        this.isGoogleLoading.set(false);
        if (response.success) {
          this.navigateAfterLogin();
        } else {
          this.errorMessage.set(response.message || 'Google login failed. Please try again.');
        }
      },
      error: (err) => {
        this.isGoogleLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Google login failed. Please try again.');
      }
    });
  }

  get emailControl() { return this.loginForm.get('email'); }
  get passwordControl() { return this.loginForm.get('password'); }

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
    if (control.errors['minlength']) return 'Password must be at least 6 characters';
    if (control.errors['maxlength']) return 'Password is too long';
    return 'Invalid password';
  }

  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  onSubmit(): void {
    this.loginForm.markAllAsTouched();
    if (this.loginForm.invalid) return;

    const email = (this.emailControl?.value || '').trim().toLowerCase();
    this.emailControl?.setValue(email, { emitEvent: false });

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService.login(this.loginForm.value).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (response.success) {
          const user = this.authService.currentUser();
          if (user?.role === UserRole.Admin) {
            this.router.navigate(['/admin/dashboard']);
          } else {
            this.router.navigate(['/dashboard']);
          }
        } else {
          this.errorMessage.set(response.message || 'Login failed');
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Connection error occurred');
      }
    });
  }

  private navigateAfterLogin(): void {
    const user = this.authService.currentUser();
    if (user?.role === UserRole.Admin) {
      this.router.navigate(['/admin/dashboard']);
    } else {
      this.router.navigate(['/dashboard']);
    }
  }
}
