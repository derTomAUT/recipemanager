import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('frontend');

  currentUrl = '/';

  constructor(
    private authService: AuthService,
    private router: Router
  ) {
    this.currentUrl = this.router.url;
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => {
        this.currentUrl = event.urlAfterRedirects;
      });
  }

  get showNavbar(): boolean {
    if (!this.authService.isAuthenticated()) {
      return false;
    }

    return !this.currentUrl.startsWith('/login') &&
      !this.currentUrl.startsWith('/household/setup');
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
