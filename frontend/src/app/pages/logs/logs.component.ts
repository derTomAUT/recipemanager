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

  private streamController?: AbortController;
  private streamTask?: Promise<void>;
  private buffered: string[] = [];

  constructor(private authService: AuthService, private zone: NgZone) {}

  ngOnInit() {
    const token = this.authService.getToken();
    if (!token) {
      this.error = 'Not authenticated.';
      this.connected = false;
      return;
    }

    const url = `${environment.apiUrl}/logs/stream`;
    this.streamController = new AbortController();
    this.streamTask = this.streamLogs(url, token, this.streamController.signal).finally(() => {
      this.streamTask = undefined;
    });
  }

  ngOnDestroy() {
    this.streamController?.abort();
    this.streamController = undefined;
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

  private async streamLogs(url: string, token: string, signal: AbortSignal): Promise<void> {
    try {
      const response = await fetch(url, {
        method: 'GET',
        headers: {
          Accept: 'text/event-stream',
          Authorization: `Bearer ${token}`
        },
        cache: 'no-store',
        signal
      });

      if (!response.ok || !response.body) {
        throw new Error(`Log stream request failed (${response.status}).`);
      }

      this.zone.run(() => {
        this.connected = true;
        this.error = '';
      });

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      while (!signal.aborted) {
        const { value, done } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        buffer = this.consumeSseBuffer(buffer);
      }

      if (!signal.aborted) {
        this.zone.run(() => {
          this.connected = false;
          this.error = 'Log stream disconnected.';
        });
      }
    } catch (error) {
      if (signal.aborted) {
        return;
      }

      const message = error instanceof Error ? error.message : 'Log stream disconnected.';
      this.zone.run(() => {
        this.connected = false;
        this.error = message;
      });
    }
  }

  private consumeSseBuffer(buffer: string): string {
    const normalized = buffer.replace(/\r\n/g, '\n');
    let remaining = normalized;
    let boundary = remaining.indexOf('\n\n');

    while (boundary >= 0) {
      const rawEvent = remaining.slice(0, boundary);
      this.handleSseEvent(rawEvent);
      remaining = remaining.slice(boundary + 2);
      boundary = remaining.indexOf('\n\n');
    }

    return remaining;
  }

  private handleSseEvent(rawEvent: string): void {
    if (!rawEvent.trim()) return;

    let eventName = 'message';
    const dataLines: string[] = [];
    for (const line of rawEvent.split('\n')) {
      if (line.startsWith('event:')) {
        eventName = line.slice('event:'.length).trim();
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice('data:'.length).trimStart());
      }
    }

    const data = dataLines.join('\n');
    this.zone.run(() => {
      if (eventName === 'error') {
        this.error = data || 'Log stream error.';
        return;
      }

      if (this.paused) {
        this.buffered.push(data);
        return;
      }

      this.appendLine(data);
    });
  }
}
