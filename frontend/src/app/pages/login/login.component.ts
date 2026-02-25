import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  template: `
    <div class="login-container">
      <h1>Recipe Manager</h1>
      <div id="g_id_onload"
           data-client_id="YOUR_GOOGLE_CLIENT_ID"
           data-callback="handleGoogleLogin">
      </div>
      <div class="g_id_signin" data-type="standard"></div>
      <p class="note">Note: Configure Google Client ID in index.html</p>
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
  `]
})
export class LoginComponent {
  constructor(private auth: AuthService, private router: Router) {
    (window as any).handleGoogleLogin = (response: any) => {
      this.auth.googleLogin(response.credential).subscribe({
        next: () => this.router.navigate(['/']),
        error: err => console.error('Login failed', err)
      });
    };
  }
}
