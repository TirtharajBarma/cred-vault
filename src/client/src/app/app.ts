import { Component, inject, signal, computed } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { NavbarComponent } from './core/components/layout/navbar/navbar.component';
import { FooterComponent } from './core/components/layout/footer/footer.component';
import { AuthService } from './core/services/auth.service';
import { filter } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NavbarComponent, FooterComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected authService = inject(AuthService);
  private router = inject(Router);

  // Track current URL to determine if we are on an admin route
  private navEnd = toSignal(
    this.router.events.pipe(filter(event => event instanceof NavigationEnd))
  );

  isAdminRoute = computed(() => {
    const event = this.navEnd() as NavigationEnd;
    return event?.urlAfterRedirects?.startsWith('/admin') ?? false;
  });
}
