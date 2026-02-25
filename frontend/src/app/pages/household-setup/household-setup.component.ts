import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-household-setup',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="setup-container">
      <h1>Set Up Your Household</h1>
      <div *ngIf="error" class="error">{{ error }}</div>
      <div class="tabs">
        <button [class.active]="tab === 'create'" (click)="tab = 'create'">Create</button>
        <button [class.active]="tab === 'join'" (click)="tab = 'join'">Join</button>
      </div>
      <div *ngIf="tab === 'create'">
        <input [(ngModel)]="householdName" placeholder="Household name" />
        <button (click)="create()">Create Household</button>
      </div>
      <div *ngIf="tab === 'join'">
        <input [(ngModel)]="inviteCode" placeholder="Invite code" />
        <button (click)="join()">Join Household</button>
      </div>
    </div>
  `,
  styles: [`
    .setup-container { max-width: 400px; margin: 4rem auto; padding: 2rem; }
    .tabs { display: flex; gap: 1rem; margin-bottom: 1rem; }
    button { padding: 0.5rem 1rem; cursor: pointer; }
    button.active { background: #007bff; color: white; }
    input { width: 100%; padding: 0.5rem; margin-bottom: 1rem; }
    .error { color: #dc3545; margin-bottom: 1rem; padding: 0.5rem; background: #f8d7da; border-radius: 4px; }
  `]
})
export class HouseholdSetupComponent {
  tab: 'create' | 'join' = 'create';
  householdName = '';
  inviteCode = '';
  error = '';

  constructor(private http: HttpClient, private router: Router) {}

  create() {
    this.error = '';
    this.http.post(`${environment.apiUrl}/household`, { name: this.householdName })
      .subscribe({
        next: () => location.reload(),
        error: () => this.error = 'Failed to create household. Please try again.'
      });
  }

  join() {
    this.error = '';
    this.http.post(`${environment.apiUrl}/household/join`, { inviteCode: this.inviteCode })
      .subscribe({
        next: () => location.reload(),
        error: () => this.error = 'Failed to join household. Check the invite code and try again.'
      });
  }
}
