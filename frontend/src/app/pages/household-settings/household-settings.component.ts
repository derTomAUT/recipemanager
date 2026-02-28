import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import * as L from 'leaflet';
import { AuthService } from '../../services/auth.service';
import {
  HouseholdActivityItem,
  HouseholdInvite,
  HouseholdMember,
  HouseholdSettingsService
} from '../../services/household-settings.service';
import { buildHouseholdInviteLink, getApiErrorMessage } from './household-settings.utils';

@Component({
  selector: 'app-household-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="settings-page">
      <header class="page-header">
        <h1>Household Settings</h1>
        <a routerLink="/home" class="btn-secondary">Back to Home</a>
      </header>

      <div *ngIf="loading" class="loading">Loading settings...</div>
      <div *ngIf="!loading && !isOwner" class="error">Only household owners can view this page.</div>
      <div *ngIf="error" class="error">{{ error }}</div>
      <div *ngIf="message" class="message">{{ message }}</div>

      <section *ngIf="!loading && isOwner" class="card">
        <h2>Invite</h2>
        <p class="help">Share this invite link with a new household member. Links expire after 5 days.</p>
        <div *ngIf="inviteInfo" class="invite-meta">
          <span>Created: {{ inviteInfo.createdAtUtc | date:'medium' }}</span>
          <span>Expires: {{ inviteInfo.expiresAtUtc | date:'medium' }}</span>
          <span class="status" [class.status-inactive]="inviteInfo.isExpired">
            {{ inviteInfo.isExpired ? 'Expired' : 'Active' }}
          </span>
        </div>
        <div class="invite-row">
          <input [value]="getInviteLink()" readonly />
          <button class="btn-secondary" type="button" (click)="copyInviteLink()">Copy Link</button>
          <button class="btn-secondary" type="button" (click)="regenerateInvite()" [disabled]="saving">
            Regenerate Link
          </button>
        </div>
      </section>

      <section *ngIf="!loading && isOwner" class="card">
        <h2>Members</h2>
        <p class="help">Owners can disable members. To disable an owner, promote another active member to Owner first.</p>
        <div *ngIf="members.length === 0" class="help">No members found.</div>
        <div *ngFor="let member of members" class="member-row">
          <div class="member-info">
            <strong>{{ member.name }}</strong>
            <span>{{ member.email }} - {{ member.role }}</span>
          </div>
          <div class="member-actions">
            <label class="role-editor">
              <span>Role</span>
              <select
                [ngModel]="roleDraftByUserId[member.id] ?? member.role"
                (ngModelChange)="setRoleDraft(member.id, $event)"
                [disabled]="saving || !member.isActive"
              >
                <option value="Owner">Owner</option>
                <option value="Member">Member</option>
                <option value="Viewer">Viewer</option>
              </select>
            </label>
            <button
              class="btn-secondary"
              type="button"
              [disabled]="saving || !member.isActive || (roleDraftByUserId[member.id] ?? member.role) === member.role"
              (click)="updateMemberRole(member)"
            >
              Update Role
            </button>
            <span class="status" [class.status-inactive]="!member.isActive">
              {{ member.isActive ? 'Active' : 'Disabled' }}
            </span>
            <button
              *ngIf="member.isActive"
              class="btn-secondary"
              type="button"
              [disabled]="saving"
              (click)="disableMember(member.id)"
            >
              Disable
            </button>
            <button
              *ngIf="!member.isActive"
              class="btn-secondary"
              type="button"
              [disabled]="saving"
              (click)="enableMember(member.id)"
            >
              Enable
            </button>
          </div>
        </div>
      </section>

      <section *ngIf="!loading && isOwner" class="card">
        <h2>Household Activity</h2>
        <div *ngIf="activity.length === 0" class="help">No activity yet.</div>
        <div *ngFor="let item of activity" class="activity-row">
          <div class="activity-event">{{ formatActivity(item) }}</div>
          <div class="activity-time">{{ item.createdAtUtc | date:'medium' }}</div>
        </div>
      </section>

      <section *ngIf="!loading && isOwner" class="card">
        <h2>Household Location</h2>
        <p class="help">Pick your household location to determine seasonal meal suggestions correctly by hemisphere.</p>
        <div class="map-shell">
          <div #locationMap class="location-map" aria-label="Household location map"></div>
        </div>
        <div class="coord-row">
          <span><strong>Latitude:</strong> {{ latitude ?? 'Not set' }}</span>
          <span><strong>Longitude:</strong> {{ longitude ?? 'Not set' }}</span>
          <button class="btn-secondary" type="button" (click)="clearCoordinates()" [disabled]="saving">
            Clear
          </button>
          <span
            *ngIf="locationSaveStatus"
            class="location-save-status"
            [class.location-save-status-success]="locationSaveStatus === 'success'"
            [class.location-save-status-error]="locationSaveStatus === 'error'"
          >
            {{ locationSaveStatus === 'success' ? 'Location saved' : 'Error when saving' }}
          </span>
        </div>
      </section>

      <section *ngIf="!loading && isOwner" class="card">
        <h2>AI Import Settings</h2>
        <p class="help">Configure an AI provider to power recipe import when JSON-LD is missing.</p>

        <label class="field">
          <span>Provider</span>
          <select [(ngModel)]="provider" (change)="onProviderChange()" [disabled]="saving">
            <option value="">Select a provider</option>
            <option value="OpenAI">OpenAI</option>
            <option value="Anthropic">Anthropic</option>
          </select>
        </label>

        <label class="field">
          <span>API Key</span>
          <input
            type="password"
            [(ngModel)]="apiKeyInput"
            [placeholder]="hasApiKey ? 'API key saved (enter to replace)' : 'Enter API key'"
            [disabled]="saving"
          />
        </label>

        <div class="field-inline">
          <button class="btn-secondary" (click)="loadModelsWithKey()" [disabled]="modelsLoading || !provider">
            {{ modelsLoading ? 'Loading models...' : 'Load models' }}
          </button>
          <span *ngIf="modelsError" class="error-inline">{{ modelsError }}</span>
        </div>

        <label class="field">
          <span>Model</span>
          <select [(ngModel)]="model" [disabled]="saving || !modelsLoaded">
            <option value="">Select a model</option>
            <option *ngFor="let m of models" [value]="m">{{ m }}</option>
          </select>
        </label>

        <div class="actions">
          <button class="btn-primary" (click)="save()" [disabled]="saveDisabled">
            {{ saving ? 'Saving...' : 'Save Settings' }}
          </button>
          <span *ngIf="!modelsLoaded && provider" class="hint">Load the model list to enable saving.</span>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .settings-page { padding: 1rem; max-width: 720px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; flex-wrap: wrap; gap: 1rem; }
    .page-header h1 { margin: 0; }
    .btn-secondary { padding: 0.5rem 1rem; min-height: 44px; display: inline-flex; align-items: center; background: var(--surface-2); color: var(--text); text-decoration: none; border-radius: var(--radius-sm); border: none; cursor: pointer; }
    .btn-primary { padding: 0.75rem 1.5rem; min-height: 44px; background: var(--primary); color: white; border: none; border-radius: var(--radius-sm); cursor: pointer; font-size: 1rem; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }

    .loading { text-align: center; padding: 2rem; }
    .error { background: color-mix(in srgb, var(--primary) 20%, var(--surface)); color: var(--text); padding: 1rem; border-radius: var(--radius-sm); margin-bottom: 1rem; }
    .message { background: color-mix(in srgb, var(--secondary) 20%, var(--surface)); color: var(--text); padding: 1rem; border-radius: var(--radius-sm); margin-bottom: 1rem; }
    .error-inline { color: var(--primary); font-size: 0.9rem; }

    .card { background: var(--surface); border: 1px solid color-mix(in srgb, var(--text) 12%, transparent); border-radius: var(--radius-md); padding: 1.5rem; box-shadow: var(--shadow-soft); margin-bottom: 1rem; }
    .card h2 { margin-top: 0; }
    .help { color: var(--muted); margin-top: 0.25rem; }

    .field { display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 1rem; }
    .field-inline { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 1rem; flex-wrap: wrap; }
    select, input { padding: 0.5rem; border: 1px solid color-mix(in srgb, var(--text) 18%, transparent); border-radius: var(--radius-sm); min-height: 40px; font-size: 1rem; background: var(--surface-2); color: var(--text); }
    input { width: 100%; }

    .actions { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }
    .hint { color: var(--muted); font-size: 0.9rem; }
    .invite-meta { display: flex; gap: 0.9rem; flex-wrap: wrap; font-size: 0.9rem; color: var(--muted); margin-bottom: 0.5rem; }
    .invite-row { display: grid; grid-template-columns: 1fr auto auto; gap: 0.5rem; margin-bottom: 0.5rem; }
    .member-row { display: flex; justify-content: space-between; align-items: center; border-top: 1px solid color-mix(in srgb, var(--text) 10%, transparent); padding: 0.75rem 0; gap: 1rem; flex-wrap: wrap; }
    .member-info { display: flex; flex-direction: column; gap: 0.125rem; }
    .member-info span { color: var(--muted); font-size: 0.9rem; }
    .member-actions { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
    .role-editor { display: flex; align-items: center; gap: 0.4rem; color: var(--muted); font-size: 0.9rem; }
    .role-editor select { min-width: 105px; }
    .status { font-size: 0.85rem; color: var(--secondary); font-weight: 600; }
    .status-inactive { color: var(--accent); }
    .activity-row { display: flex; justify-content: space-between; align-items: baseline; gap: 1rem; border-top: 1px solid color-mix(in srgb, var(--text) 10%, transparent); padding: 0.6rem 0; }
    .activity-event { font-weight: 600; }
    .activity-time { color: var(--muted); font-size: 0.85rem; text-align: right; }
    .map-shell { border: 1px solid color-mix(in srgb, var(--text) 16%, transparent); border-radius: var(--radius-sm); overflow: hidden; background: var(--surface-2); }
    .location-map { width: 100%; height: 280px; }
    .coord-row { margin-top: 0.65rem; display: flex; gap: 1rem; align-items: center; flex-wrap: wrap; color: var(--muted); }
    .location-save-status { font-size: 0.9rem; font-weight: 600; }
    .location-save-status-success { color: var(--secondary); }
    .location-save-status-error { color: var(--primary); }
  `]
})
export class HouseholdSettingsComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('locationMap')
  set locationMapRef(value: ElementRef<HTMLDivElement> | undefined) {
    this.locationMap = value;
    this.tryInitMap();
  }

  private locationMap?: ElementRef<HTMLDivElement>;
  loading = true;
  saving = false;
  modelsLoading = false;
  modelsLoaded = false;
  error = '';
  message = '';
  modelsError = '';

  isOwner = false;
  provider = '';
  model = '';
  apiKeyInput = '';
  hasApiKey = false;
  models: string[] = [];
  householdInviteCode = '';
  inviteInfo: HouseholdInvite | null = null;
  members: HouseholdMember[] = [];
  activity: HouseholdActivityItem[] = [];
  roleDraftByUserId: Record<string, string | undefined> = {};
  latitude: number | null = null;
  longitude: number | null = null;
  private map?: L.Map;
  private marker?: L.CircleMarker;
  private viewInitialized = false;
  private locationSaveTimer?: ReturnType<typeof setTimeout>;
  private locationStatusHideTimer?: ReturnType<typeof setTimeout>;
  locationSaveStatus: 'success' | 'error' | null = null;

  constructor(
    private authService: AuthService,
    private settingsService: HouseholdSettingsService,
    private router: Router
  ) {}

  ngOnInit() {
    this.authService.user$.subscribe(user => {
      if (!user) return;
      this.isOwner = user.role === 'Owner';
      if (!this.isOwner) {
        this.loading = false;
        this.router.navigate(['/home']);
        return;
      }
      this.loadHousehold();
      this.loadInvite();
      this.loadActivity();
      this.loadSettings();
    });
  }

  ngAfterViewInit() {
    this.viewInitialized = true;
    this.tryInitMap();
  }

  ngOnDestroy() {
    if (this.locationSaveTimer) {
      clearTimeout(this.locationSaveTimer);
    }
    if (this.locationStatusHideTimer) {
      clearTimeout(this.locationStatusHideTimer);
    }
    this.map?.remove();
  }

  get saveDisabled(): boolean {
    if (this.saving || !this.provider || !this.model) return true;
    if (!this.modelsLoaded) return true;
    return false;
  }

  loadSettings() {
    this.loading = true;
    this.error = '';
    this.settingsService.getSettings().subscribe({
      next: (settings) => {
        this.provider = settings.aiProvider ?? '';
        this.model = settings.aiModel ?? '';
        this.hasApiKey = settings.hasApiKey;
        this.latitude = settings.latitude ?? null;
        this.longitude = settings.longitude ?? null;
        this.loading = false;
        this.tryInitMap();
        this.updateMapMarkerFromState();

        if (this.provider && this.hasApiKey) {
          this.loadModels();
        }
      },
      error: () => {
        this.error = 'Failed to load household settings';
        this.loading = false;
      }
    });
  }

  loadHousehold() {
    this.settingsService.getHousehold().subscribe({
      next: (household) => {
        this.householdInviteCode = household.inviteCode;
        this.members = household.members;
        this.roleDraftByUserId = {};
        for (const member of household.members) {
          this.roleDraftByUserId[member.id] = member.role;
        }
      },
      error: () => {
        this.error = 'Failed to load household members';
      }
    });
  }

  loadInvite() {
    this.settingsService.getInvite().subscribe({
      next: (invite) => {
        this.inviteInfo = invite;
        this.householdInviteCode = invite.inviteCode;
      },
      error: () => {
        this.error = 'Failed to load invite details';
      }
    });
  }

  loadActivity() {
    this.settingsService.getActivity().subscribe({
      next: (items) => this.activity = items,
      error: () => this.error = 'Failed to load household activity'
    });
  }

  onProviderChange() {
    this.models = [];
    this.modelsLoaded = false;
    this.modelsError = '';
    this.model = '';

    if (this.provider && this.hasApiKey) {
      this.loadModels();
    }
  }

  loadModelsWithKey() {
    this.modelsError = '';
    this.message = '';

    if (!this.provider) {
      this.modelsError = 'Select a provider first.';
      return;
    }

    const apiKey = this.apiKeyInput.trim();
    if (!this.hasApiKey && !apiKey) {
      this.modelsError = 'Enter an API key to load models.';
      return;
    }

    if (apiKey) {
      this.saving = true;
      this.settingsService.updateSettings({
        aiProvider: this.provider,
        apiKey
      }).subscribe({
        next: (settings) => {
          this.hasApiKey = settings.hasApiKey;
          this.apiKeyInput = '';
          this.saving = false;
          this.loadModels();
        },
        error: () => {
          this.modelsError = 'Failed to save API key.';
          this.saving = false;
        }
      });
      return;
    }

    this.loadModels();
  }

  loadModels() {
    this.modelsLoading = true;
    this.modelsLoaded = false;
    this.modelsError = '';
    this.settingsService.getModels(this.provider).subscribe({
      next: (models) => {
        this.models = models;
        this.modelsLoaded = true;
        if (!this.model && models.length > 0) {
          this.model = models[0];
        }
        this.modelsLoading = false;
      },
      error: () => {
        this.modelsError = 'Failed to load models. Ensure your API key is set.';
        this.modelsLoading = false;
      }
    });
  }

  save() {
    if (this.saveDisabled) return;

    this.saving = true;
    this.error = '';
    this.message = '';

    this.settingsService.updateSettings({
      aiProvider: this.provider,
      aiModel: this.model,
      apiKey: this.apiKeyInput.trim() ? this.apiKeyInput.trim() : undefined,
      latitude: this.latitude ?? undefined,
      longitude: this.longitude ?? undefined
    }).subscribe({
      next: (settings) => {
        this.hasApiKey = settings.hasApiKey;
        this.latitude = settings.latitude ?? null;
        this.longitude = settings.longitude ?? null;
        this.apiKeyInput = '';
        this.message = 'Settings saved.';
        this.saving = false;
        this.updateMapMarkerFromState();
      },
      error: (error) => {
        this.error = getApiErrorMessage(error, 'Failed to save settings');
        this.saving = false;
      }
    });
  }

  getInviteLink(origin: string = window.location.origin): string {
    return buildHouseholdInviteLink(this.householdInviteCode, origin);
  }

  copyInviteLink() {
    const link = this.getInviteLink();
    if (!link) return;

    navigator.clipboard.writeText(link)
      .then(() => this.message = 'Invite link copied.')
      .catch(() => this.error = 'Failed to copy invite link.');
  }

  regenerateInvite() {
    this.saving = true;
    this.settingsService.regenerateInvite().subscribe({
      next: (invite) => {
        this.inviteInfo = invite;
        this.householdInviteCode = invite.inviteCode;
        this.message = 'Invite link regenerated.';
        this.error = '';
        this.saving = false;
        this.loadActivity();
      },
      error: (error) => {
        this.error = getApiErrorMessage(error, 'Failed to regenerate invite link.');
        this.saving = false;
      }
    });
  }

  setRoleDraft(userId: string, role: string) {
    this.roleDraftByUserId[userId] = role;
  }

  updateMemberRole(member: HouseholdMember) {
    const nextRole = this.roleDraftByUserId[member.id] ?? member.role;
    if (nextRole === member.role) return;

    this.saving = true;
    this.settingsService.updateMemberRole(member.id, nextRole).subscribe({
      next: () => {
        this.message = `Role updated to ${nextRole}.`;
        this.error = '';
        this.saving = false;
        this.loadHousehold();
        this.loadActivity();
      },
      error: (error) => {
        this.error = getApiErrorMessage(error, 'Failed to update role.');
        this.saving = false;
      }
    });
  }

  disableMember(userId: string) {
    this.saving = true;
    this.settingsService.disableMember(userId).subscribe({
      next: () => {
        this.message = 'Member disabled.';
        this.error = '';
        this.saving = false;
        this.loadHousehold();
        this.loadActivity();
      },
      error: (error) => {
        this.error = getApiErrorMessage(error, 'Failed to disable member');
        this.saving = false;
      }
    });
  }

  enableMember(userId: string) {
    this.saving = true;
    this.settingsService.enableMember(userId).subscribe({
      next: () => {
        this.message = 'Member enabled.';
        this.error = '';
        this.saving = false;
        this.loadHousehold();
        this.loadActivity();
      },
      error: (error) => {
        this.error = getApiErrorMessage(error, 'Failed to enable member');
        this.saving = false;
      }
    });
  }

  formatActivity(item: HouseholdActivityItem): string {
    switch (item.eventType) {
      case 'InviteRegenerated':
        return 'Invite link regenerated';
      case 'MemberDisabled':
        return 'Member disabled';
      case 'MemberEnabled':
        return 'Member re-enabled';
      case 'MemberRoleUpdated':
        return item.details ? `Role changed (${item.details})` : 'Member role changed';
      case 'MemberJoined':
        return 'Member joined household';
      case 'MemberRemoved':
        return 'Member removed';
      case 'HouseholdCreated':
        return 'Household created';
      default:
        return item.details || item.eventType;
    }
  }

  clearCoordinates() {
    this.latitude = null;
    this.longitude = null;
    this.marker?.remove();
    this.marker = undefined;
    if (this.map) {
      this.map.setView([20, 0], 2);
    }
    this.schedulePersistLocation();
  }

  private tryInitMap() {
    if (!this.viewInitialized || this.map || !this.locationMap?.nativeElement) {
      return;
    }

    const centerLat = this.latitude ?? 20;
    const centerLng = this.longitude ?? 0;
    const zoom = this.latitude != null && this.longitude != null ? 8 : 2;
    this.map = L.map(this.locationMap.nativeElement, {
      zoomControl: true
    }).setView([centerLat, centerLng], zoom);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(this.map);

    this.map.on('click', (event: L.LeafletMouseEvent) => {
      this.latitude = Number(event.latlng.lat.toFixed(6));
      this.longitude = Number(event.latlng.lng.toFixed(6));
      this.updateMapMarkerFromState();
      this.schedulePersistLocation();
    });

    this.updateMapMarkerFromState();
    setTimeout(() => this.map?.invalidateSize(), 0);
  }

  private updateMapMarkerFromState() {
    if (!this.map || this.latitude == null || this.longitude == null) {
      return;
    }

    if (!this.marker) {
      this.marker = L.circleMarker([this.latitude, this.longitude], {
        radius: 7,
        color: '#1d7f5f',
        weight: 2,
        fillColor: '#2db28f',
        fillOpacity: 0.9
      }).addTo(this.map);
    } else {
      this.marker.setLatLng([this.latitude, this.longitude]);
    }
  }

  private schedulePersistLocation() {
    if (this.locationSaveTimer) {
      clearTimeout(this.locationSaveTimer);
    }

    this.locationSaveTimer = setTimeout(() => this.persistLocation(), 250);
  }

  private persistLocation() {
    this.clearLocationStatus();
    this.settingsService.updateLocation({
      latitude: this.latitude ?? undefined,
      longitude: this.longitude ?? undefined
    }).subscribe({
      next: (settings) => {
        this.latitude = settings.latitude ?? null;
        this.longitude = settings.longitude ?? null;
        this.showLocationStatus('success');
      },
      error: (error) => {
        this.error = getApiErrorMessage(error, 'Failed to save location');
        this.showLocationStatus('error');
      }
    });
  }

  private showLocationStatus(status: 'success' | 'error') {
    this.locationSaveStatus = status;
    if (this.locationStatusHideTimer) {
      clearTimeout(this.locationStatusHideTimer);
    }

    this.locationStatusHideTimer = setTimeout(() => {
      this.locationSaveStatus = null;
      this.locationStatusHideTimer = undefined;
    }, 5000);
  }

  private clearLocationStatus() {
    if (this.locationStatusHideTimer) {
      clearTimeout(this.locationStatusHideTimer);
      this.locationStatusHideTimer = undefined;
    }
    this.locationSaveStatus = null;
  }
}
