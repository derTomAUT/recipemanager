import { Component, OnDestroy, AfterViewInit } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../services/auth.service';
import { environment } from '../../../environments/environment';

declare const google: any;

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="login-container">
      <h1>Recipe Manager</h1>
      <div *ngIf="error" class="error">{{ error }}</div>
      <div id="g_id_signin"></div>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100vh;
      gap: 2rem;
    }
    .note { color: #666; font-size: 0.875rem; }
    .error { color: #dc3545; padding: 0.5rem 1rem; background: #f8d7da; border-radius: 4px; }
  `]
})
export class LoginComponent implements AfterViewInit, OnDestroy {
  error = '';

  constructor(private auth: AuthService, private router: Router) {}

  ngAfterViewInit() {
    this.initGoogleSignIn();
  }

  private initGoogleSignIn() {
    if (typeof google === 'undefined') {
      // Wait for Google script to load
      setTimeout(() => this.initGoogleSignIn(), 100);
      return;
    }

    google.accounts.id.initialize({
      client_id: environment.googleClientId,
      callback: (response: any) => this.handleGoogleLogin(response)
    });

    google.accounts.id.renderButton(
      document.getElementById('g_id_signin'),
      { theme: 'outline', size: 'large', width: 280 }
    );
  }

  private handleGoogleLogin(response: any) {
    this.error = '';
    this.auth.googleLogin(response.credential).subscribe({
      next: () => this.router.navigate(['/']),
      error: () => this.error = 'Login failed. Please try again.'
    });
  }

  ngOnDestroy() {}
}
