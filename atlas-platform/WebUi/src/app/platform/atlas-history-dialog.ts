// COPY to the target: WebUi/Source/App/platform/. The audit history of one record (v1/GetAuditLog, spec 09, 4.5).
// All values are shown as text (no HTML).
import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { DialogComponent, LoaderComponent } from './atlas-pbdev';
import { AtlasApiService } from './atlas-api.service';
import { CPAuditChange, CPAuditLog } from './atlas-models';

interface HistoryEntry {
  event: string;
  by: string;
  when: Date | string;
  changes: CPAuditChange[];
}

@Component({
  selector: 'atlas-history-dialog',
  imports: [DialogComponent, LoaderComponent, DatePipe],
  template: `<pbdev-dialog [visible]="open" [header]="'History of record ' + recordId" (visibleChange)="!$event && closed.emit()">
    @if (loading()) {
      <pbdev-loader />
    } @else if (entries().length === 0) {
      <p>No history.</p>
    } @else {
      @for (entry of entries(); track $index) {
        <div class="atlas-history-entry">
          <div class="atlas-history-head">
            <span class="atlas-badge" [class]="'atlas-badge atlas-badge-' + entry.event.toLowerCase()">{{ entry.event }}</span>
            <span>{{ entry.by }}</span>
            <span class="atlas-muted">{{ entry.when | date: 'dd-MMM-yyyy HH:mm' }}</span>
          </div>
          @if (entry.changes.length > 0) {
            <table class="atlas-table">
              <thead><tr><th>Field</th><th>Old value</th><th>New value</th></tr></thead>
              <tbody>
                @for (change of entry.changes; track change.Name) {
                  <tr><td>{{ change.Label || change.Name }}</td><td>{{ change.Old ?? '' }}</td><td>{{ change.Value ?? '' }}</td></tr>
                }
              </tbody>
            </table>
          }
        </div>
      }
    }
  </pbdev-dialog>`,
})
export class AtlasHistoryDialog implements OnChanges {
  @Input() open = false;
  @Input({ required: true }) appCode = '';
  @Input({ required: true }) recordId = 0;
  @Output() closed = new EventEmitter<void>();

  private readonly api = inject(AtlasApiService);
  readonly loading = signal(false);
  readonly entries = signal<HistoryEntry[]>([]);

  ngOnChanges(): void {
    if (!this.open || !this.recordId) {
      return;
    }
    this.loading.set(true);
    this.api.auditLog(this.appCode, this.recordId).subscribe({
      next: (logs) => {
        this.entries.set(logs.map((x) => toEntry(x)));
        this.loading.set(false);
      },
      error: () => {
        this.entries.set([]);
        this.loading.set(false);
      },
    });
  }
}

export function toEntry(log: CPAuditLog): HistoryEntry {
  const items = Array.isArray(log.LogMessage) ? (log.LogMessage as CPAuditChange[]) : [];
  return {
    event: log.Event ?? 'Change',
    by: log.CreatedBy,
    when: log.CreatedDateUtc ? new Date(log.CreatedDateUtc) : log.CreatedDate,
    changes: items.filter((x) => x.Name !== '_Event'),
  };
}
