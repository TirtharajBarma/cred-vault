import { Component, inject, signal, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css'
})
export class NavbarComponent {
  authService = inject(AuthService);
  private router = inject(Router);
  private eRef = inject(ElementRef);
  
  user = this.authService.currentUser;
  isProfileOpen = signal(false);
  isMobileMenuOpen = signal(false);

  toggleProfile(): void {
    this.isProfileOpen.update(v => !v);
    if (this.isProfileOpen()) this.isMobileMenuOpen.set(false);
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen.update(v => !v);
    if (this.isMobileMenuOpen()) this.isProfileOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  clickout(event: any) {
    if (!this.eRef.nativeElement.contains(event.target)) {
      this.isProfileOpen.set(false);
    }
  }

  logout(): void {
    this.authService.logout();
    this.isProfileOpen.set(false);
  }
}
