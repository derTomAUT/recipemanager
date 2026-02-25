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
  `]
})
export class HouseholdSetupComponent {
  tab: 'create' | 'join' = 'create';
  householdName = '';
  inviteCode = '';

  constructor(private http: HttpClient, private router: Router) {}

  create() {
    this.http.post(`${environment.apiUrl}/household`, { name: this.householdName })
      .subscribe(() => {
        location.reload();
      });
  }

  join() {
    this.http.post(`${environment.apiUrl}/household/join`, { inviteCode: this.inviteCode })
      .subscribe(() => {
        location.reload();
      });
  }
}
