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
  success: SuccessFilter = 'all';
  expandedRequest = new Set<string>();
  expandedResponse = new Set<string>();

  // Logs tab
  lines: string[] = [];
  connected = false;
  paused = false;
  autoScroll = true;
  logsError = '';
  private eventSource?: EventSource;
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
    if (this.eventSource) return;

    const token = this.authService.getToken();
    const url = `${environment.apiUrl}/logs/stream?access_token=${encodeURIComponent(token ?? '')}`;

    this.eventSource = new EventSource(url);
    this.eventSource.onopen = () => {
      this.zone.run(() => {
        this.connected = true;
        this.logsError = '';
      });
    };
    this.eventSource.onerror = () => {
      this.zone.run(() => {
        this.connected = false;
        this.logsError = 'Log stream disconnected. Retrying...';
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
  }

  private closeLogStream(): void {
    this.eventSource?.close();
    this.eventSource = undefined;
    this.connected = false;
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
