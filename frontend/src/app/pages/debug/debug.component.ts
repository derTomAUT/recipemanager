import { CommonModule } from '@angular/common';
import { Component, ElementRef, NgZone, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AiDebugLogEntry } from '../../models/debug.model';
import { AuthService } from '../../services/auth.service';
import { DebugService } from '../../services/debug.service';

type DebugTab = 'ai' | 'logs';
type SuccessFilter = 'all' | 'true' | 'false';

@Component({
  selector: 'app-debug',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './debug.component.html',
  styleUrl: './debug.component.css'
})
export class DebugComponent implements OnInit, OnDestroy {
  @ViewChild('logContainer') logContainer?: ElementRef<HTMLDivElement>;

  activeTab: DebugTab = 'ai';

  // AI tab
  aiEntries: AiDebugLogEntry[] = [];
  aiLoading = false;
  aiError = '';
  page = 1;
  pageSize = 50;
  totalCount = 0;
  provider = '';
  operation = '';
  operations: string[] = [];
  operationsError = '';
  success: SuccessFilter = 'all';
  expandedRequest = new Set<string>();
  expandedResponse = new Set<string>();

  // Logs tab
  lines: string[] = [];
  connected = false;
  paused = false;
  autoScroll = true;
  logsError = '';
  private streamController?: AbortController;
  private streamTask?: Promise<void>;
  private buffered: string[] = [];

  constructor(
    private debugService: DebugService,
    private authService: AuthService,
    private zone: NgZone,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const tab = this.route.snapshot.queryParamMap.get('tab');
    this.activeTab = tab === 'logs' ? 'logs' : 'ai';

    this.route.queryParamMap.subscribe(params => {
      const nextTab = params.get('tab') === 'logs' ? 'logs' : 'ai';
      if (nextTab !== this.activeTab) {
        this.activeTab = nextTab;
        this.handleTabChanged();
      }
    });

    this.loadOperations();
    this.handleTabChanged();
  }

  ngOnDestroy(): void {
    this.closeLogStream();
  }

  setTab(tab: DebugTab): void {
    if (tab === this.activeTab) return;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge'
    });
  }

  private handleTabChanged(): void {
    if (this.activeTab === 'ai') {
      this.closeLogStream();
      this.loadAiLogs(1);
      return;
    }
    this.connectLogStream();
  }

  loadAiLogs(page = this.page): void {
    this.aiLoading = true;
    this.aiError = '';
    this.page = page;

    const success = this.success === 'all' ? undefined : this.success === 'true';

    this.debugService.getAiLogs({
      provider: this.provider.trim() || undefined,
      operation: this.operation.trim() || undefined,
      success,
      page: this.page,
      pageSize: this.pageSize
    }).subscribe({
      next: (result) => {
        this.aiEntries = result.items;
        this.totalCount = result.totalCount;
        this.aiLoading = false;
      },
      error: () => {
        this.aiError = 'Failed to load AI debug logs.';
        this.aiLoading = false;
      }
    });
  }

  applyFilters(): void {
    this.loadAiLogs(1);
  }

  clearFilters(): void {
    this.provider = '';
    this.operation = '';
    this.success = 'all';
    this.loadAiLogs(1);
  }

  private loadOperations(): void {
    this.operationsError = '';
    this.debugService.getAiOperations().subscribe({
      next: (operations) => {
        this.operations = operations;
      },
      error: () => {
        this.operations = [];
        this.operationsError = 'Could not load operation list.';
      }
    });
  }

  get hasPreviousPage(): boolean {
    return this.page > 1;
  }

  get hasNextPage(): boolean {
    return this.page * this.pageSize < this.totalCount;
  }

  previousPage(): void {
    if (this.hasPreviousPage) {
      this.loadAiLogs(this.page - 1);
    }
  }

  nextPage(): void {
    if (this.hasNextPage) {
      this.loadAiLogs(this.page + 1);
    }
  }

  toggleRequest(id: string): void {
    if (this.expandedRequest.has(id)) {
      this.expandedRequest.delete(id);
      return;
    }
    this.expandedRequest.add(id);
  }

  toggleResponse(id: string): void {
    if (this.expandedResponse.has(id)) {
      this.expandedResponse.delete(id);
      return;
    }
    this.expandedResponse.add(id);
  }

  isRequestExpanded(id: string): boolean {
    return this.expandedRequest.has(id);
  }

  isResponseExpanded(id: string): boolean {
    return this.expandedResponse.has(id);
  }

  formatJson(text: string): string {
    if (!text) return '';
    try {
      return JSON.stringify(JSON.parse(text), null, 2);
    } catch {
      return text;
    }
  }

  private connectLogStream(): void {
    if (this.streamTask) return;

    const token = this.authService.getToken();
    if (!token) {
      this.connected = false;
      this.logsError = 'Not authenticated.';
      return;
    }

    const url = `${environment.apiUrl}/logs/stream`;
    this.streamController = new AbortController();
    this.streamTask = this.streamLogs(url, token, this.streamController.signal).finally(() => {
      this.streamTask = undefined;
    });
  }

  private closeLogStream(): void {
    this.streamController?.abort();
    this.streamController = undefined;
    this.connected = false;
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
        this.logsError = '';
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
          this.logsError = 'Log stream disconnected.';
        });
      }
    } catch (error) {
      if (signal.aborted) {
        return;
      }

      const message = error instanceof Error ? error.message : 'Log stream disconnected.';
      this.zone.run(() => {
        this.connected = false;
        this.logsError = message;
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
        this.logsError = data || 'Log stream error.';
        return;
      }

      if (this.paused) {
        this.buffered.push(data);
        return;
      }

      this.appendLine(data);
    });
  }

  togglePause(): void {
    this.paused = !this.paused;
    if (!this.paused && this.buffered.length > 0) {
      for (const line of this.buffered) {
        this.appendLine(line);
      }
      this.buffered = [];
    }
  }

  clearLogs(): void {
    this.lines = [];
    this.buffered = [];
  }

  toggleAutoScroll(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.autoScroll = input.checked;
  }

  private appendLine(line: string): void {
    this.lines.push(line);
    if (this.lines.length > 1500) {
      this.lines.splice(0, this.lines.length - 1500);
    }
    if (this.autoScroll && this.logContainer) {
      const element = this.logContainer.nativeElement;
      queueMicrotask(() => {
        element.scrollTop = element.scrollHeight;
      });
    }
  }
}
