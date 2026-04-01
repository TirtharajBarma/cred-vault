import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css'
})
export class ProfileComponent implements OnInit {
  private authService = inject(AuthService);

  user = this.authService.currentUser;
  
  isEditingProfile = signal(false);
  isChangingPassword = signal(false);
  
  profileForm = {
    fullName: ''
  };
  
  passwordForm = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  };
  
  isSubmittingProfile = signal(false);
  isSubmittingPassword = signal(false);
  
  profileSuccess = signal<string | null>(null);
  profileError = signal<string | null>(null);
  
  passwordSuccess = signal<string | null>(null);
  passwordError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadProfile();
  }

  loadProfile(): void {
    this.authService.getProfile().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.profileForm.fullName = res.data.fullName || '';
        }
      }
    });
  }

  toggleEditProfile(): void {
    this.isEditingProfile.set(!this.isEditingProfile());
    this.profileSuccess.set(null);
    this.profileError.set(null);
    if (this.isEditingProfile()) {
      this.profileForm.fullName = this.user()?.fullName || '';
    }
  }

  saveProfile(): void {
    if (!this.profileForm.fullName.trim()) {
      this.profileError.set('Name is required');
      return;
    }

    this.isSubmittingProfile.set(true);
    this.profileError.set(null);

    this.authService.updateProfile({ fullName: this.profileForm.fullName }).subscribe({
      next: (res) => {
        this.isSubmittingProfile.set(false);
        if (res.success) {
          this.profileSuccess.set('Profile updated successfully!');
          this.isEditingProfile.set(false);
          const currentUser = this.user();
          if (currentUser && res.data) {
            this.authService.currentUser.set({ ...currentUser, fullName: res.data.fullName });
          }
          setTimeout(() => this.profileSuccess.set(null), 3000);
        } else {
          this.profileError.set(res.message || 'Failed to update profile');
        }
      },
      error: () => {
        this.isSubmittingProfile.set(false);
        this.profileError.set('Server error');
      }
    });
  }

  toggleChangePassword(): void {
    this.isChangingPassword.set(!this.isChangingPassword());
    this.passwordSuccess.set(null);
    this.passwordError.set(null);
    this.passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
  }

  changePassword(): void {
    if (!this.passwordForm.currentPassword) {
      this.passwordError.set('Current password is required');
      return;
    }
    if (this.passwordForm.newPassword.length < 8) {
      this.passwordError.set('New password must be at least 8 characters');
      return;
    }
    if (this.passwordForm.newPassword !== this.passwordForm.confirmPassword) {
      this.passwordError.set('Passwords do not match');
      return;
    }

    this.isSubmittingPassword.set(true);
    this.passwordError.set(null);

    this.authService.changePassword({
      currentPassword: this.passwordForm.currentPassword,
      newPassword: this.passwordForm.newPassword
    }).subscribe({
      next: (res) => {
        this.isSubmittingPassword.set(false);
        if (res.success) {
          this.passwordSuccess.set('Password changed successfully!');
          this.isChangingPassword.set(false);
          this.passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
          setTimeout(() => this.passwordSuccess.set(null), 3000);
        } else {
          this.passwordError.set(res.message || 'Failed to change password');
        }
      },
      error: () => {
        this.isSubmittingPassword.set(false);
        this.passwordError.set('Server error');
      }
    });
  }
}
