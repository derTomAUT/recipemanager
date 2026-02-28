import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { SwUpdate, VersionEvent } from '@angular/service-worker';
import { AuthService } from './services/auth.service';
import { BookOpenText, Bug, ChefHat, House, LogOut, LucideAngularModule, Settings } from 'lucide-angular';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, LucideAngularModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('frontend');
  readonly homeIcon = House;
  readonly recipesIcon = BookOpenText;
  readonly householdIcon = Settings;
  readonly mealAssistantIcon = ChefHat;
  readonly debugIcon = Bug;
  readonly logoutIcon = LogOut;
  updateAvailable = false;
  activatingUpdate = false;

  currentUrl = '/';

  constructor(
    private authService: AuthService,
    private router: Router,
    private swUpdate: SwUpdate
  ) {
    this.currentUrl = this.router.url;
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => {
        this.currentUrl = event.urlAfterRedirects;
      });

    if (this.swUpdate.isEnabled) {
      this.swUpdate.versionUpdates.subscribe((event: VersionEvent) => {
        if (event.type === 'VERSION_READY') {
          this.updateAvailable = true;
        }
      });
      void this.swUpdate.checkForUpdate();
    }
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

  dismissUpdatePrompt() {
    this.updateAvailable = false;
  }

  async refreshToLatest() {
    if (!this.swUpdate.isEnabled || this.activatingUpdate) return;
    this.activatingUpdate = true;
    const separator = location.href.includes('?') ? '&' : '?';
    location.assign(`${location.href}${separator}v=${Date.now()}`);
  }
}
