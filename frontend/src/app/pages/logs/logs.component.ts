import { Component, ElementRef, NgZone, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-logs',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="logs-page">
      <header class="page-header">
        <h1>Live Logs</h1>
        <div class="header-actions">
          <span class="status" [class.connected]="connected">{{ connected ? 'Connected' : 'Disconnected' }}</span>
          <a routerLink="/home" class="btn-secondary">Back to Home</a>
        </div>
      </header>

      <div class="controls">
        <button class="btn-secondary" (click)="togglePause()">
          {{ paused ? 'Resume' : 'Pause' }}
        </button>
        <button class="btn-secondary" (click)="clear()">Clear</button>
        <label class="auto-scroll">
          <input type="checkbox" [checked]="autoScroll" (change)="toggleAutoScroll($event)" />
          Auto-scroll
        </label>
      </div>

      <div class="log-container" #logContainer>
        <div *ngFor="let line of lines" class="log-line">{{ line }}</div>
        <div *ngIf="lines.length === 0" class="empty">No log lines yet.</div>
      </div>

      <div *ngIf="error" class="error">{{ error }}</div>
    </div>
  `,
  styles: [`
    .logs-page { padding: 1rem; max-width: 1100px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; gap: 1rem; flex-wrap: wrap; }
    .page-header h1 { margin: 0; }
    .header-actions { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }
    .btn-secondary { padding: 0.5rem 1rem; min-height: 40px; display: inline-flex; align-items: center; background: #6c757d; color: white; text-decoration: none; border-radius: 4px; border: none; cursor: pointer; }
    .status { font-size: 0.9rem; color: #666; }
    .status.connected { color: #155724; }

    .controls { display: flex; gap: 0.75rem; align-items: center; flex-wrap: wrap; margin-bottom: 1rem; }
    .auto-scroll { display: inline-flex; align-items: center; gap: 0.5rem; color: #444; }

    .log-container { border: 1px solid #ddd; border-radius: 6px; background: #0b0f14; color: #d0d6dc; padding: 0.75rem; height: 60vh; overflow-y: auto; font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace; font-size: 0.85rem; }
    .log-line { white-space: pre-wrap; word-break: break-word; padding: 0.1rem 0; }
    .empty { color: #8a8f98; text-align: center; padding: 2rem; }
    .error { background: #f8d7da; color: #721c24; padding: 0.75rem 1rem; border-radius: 4px; margin-top: 1rem; }
  `]
})
export class LogsComponent implements OnInit, OnDestroy {
  @ViewChild('logContainer') logContainer?: ElementRef<HTMLDivElement>;

  lines: string[] = [];
  connected = false;
  paused = false;
  autoScroll = true;
  error = '';

  private eventSource?: EventSource;
  private buffered: string[] = [];

  constructor(private authService: AuthService, private zone: NgZone) {}

  ngOnInit() {
    const token = this.authService.getToken();
    const url = `${environment.apiUrl}/logs/stream?access_token=${encodeURIComponent(token ?? '')}`;

    this.eventSource = new EventSource(url);
    this.eventSource.onopen = () => {
      this.zone.run(() => {
        this.connected = true;
        this.error = '';
      });
    };
    this.eventSource.onerror = () => {
      this.zone.run(() => {
        this.connected = false;
        this.error = 'Log stream disconnected. Retrying...';
      });
    };
    this.eventSource.onmessage = (event) => {
      this.zone.run(() => {
        if (this.paused) {
          this.buffered.push(event.data);
          return;
        }
        this.appendLine(event.data);
      });
    };
    this.eventSource.addEventListener('error', (event) => {
      this.zone.run(() => {
        if ((event as MessageEvent).data) {
          this.error = (event as MessageEvent).data;
        }
      });
    });
  }

  ngOnDestroy() {
    this.eventSource?.close();
  }

  togglePause() {
    this.paused = !this.paused;
    if (!this.paused && this.buffered.length > 0) {
      for (const line of this.buffered) {
        this.appendLine(line);
      }
      this.buffered = [];
    }
  }

  clear() {
    this.lines = [];
    this.buffered = [];
  }

  toggleAutoScroll(event: Event) {
    const input = event.target as HTMLInputElement;
    this.autoScroll = input.checked;
  }

  private appendLine(line: string) {
    this.lines.push(line);
    if (this.lines.length > 1000) {
      this.lines.splice(0, this.lines.length - 1000);
    }
    if (this.autoScroll && this.logContainer) {
      const el = this.logContainer.nativeElement;
      queueMicrotask(() => {
        el.scrollTop = el.scrollHeight;
      });
    }
  }
}
