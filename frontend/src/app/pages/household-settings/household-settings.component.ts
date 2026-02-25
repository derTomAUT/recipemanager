import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { HouseholdSettingsService } from '../../services/household-settings.service';

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
    .btn-secondary { padding: 0.5rem 1rem; min-height: 44px; display: inline-flex; align-items: center; background: #6c757d; color: white; text-decoration: none; border-radius: 4px; border: none; cursor: pointer; }
    .btn-primary { padding: 0.75rem 1.5rem; min-height: 44px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 1rem; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }

    .loading { text-align: center; padding: 2rem; }
    .error { background: #f8d7da; color: #721c24; padding: 1rem; border-radius: 4px; margin-bottom: 1rem; }
    .message { background: #d4edda; color: #155724; padding: 1rem; border-radius: 4px; margin-bottom: 1rem; }
    .error-inline { color: #dc3545; font-size: 0.9rem; }

    .card { background: #fff; border: 1px solid #ddd; border-radius: 8px; padding: 1.5rem; }
    .card h2 { margin-top: 0; }
    .help { color: #666; margin-top: 0.25rem; }

    .field { display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 1rem; }
    .field-inline { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 1rem; flex-wrap: wrap; }
    select, input { padding: 0.5rem; border: 1px solid #ccc; border-radius: 4px; min-height: 40px; font-size: 1rem; }
    input { width: 100%; }

    .actions { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }
    .hint { color: #666; font-size: 0.9rem; }
  `]
})
export class HouseholdSettingsComponent implements OnInit {
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
      this.loadSettings();
    });
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
        this.loading = false;

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

    if (!this.hasApiKey && !this.apiKeyInput.trim()) {
      this.modelsError = 'Enter an API key to load models.';
      return;
    }

    if (!this.hasApiKey && this.apiKeyInput.trim()) {
      this.saving = true;
      this.settingsService.updateSettings({
        aiProvider: this.provider,
        apiKey: this.apiKeyInput.trim()
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
      apiKey: this.apiKeyInput.trim() ? this.apiKeyInput.trim() : undefined
    }).subscribe({
      next: (settings) => {
        this.hasApiKey = settings.hasApiKey;
        this.apiKeyInput = '';
        this.message = 'Settings saved.';
        this.saving = false;
      },
      error: () => {
        this.error = 'Failed to save settings';
        this.saving = false;
      }
    });
  }
}
