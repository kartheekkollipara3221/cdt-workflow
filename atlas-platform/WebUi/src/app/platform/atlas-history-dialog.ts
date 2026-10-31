// COPY to the target: WebUi/src/app/platform/. The audit history of one record (v1/GetAuditLog, spec 09, 4.5).
// All values are shown as text (no HTML).
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, of, switchMap } from 'rxjs';
import { problemText } from './atlas-errors';
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
  changeDetection: ChangeDetectionStrategy.OnPush, // review NG-11
  template: `<pbdev-dialog [visible]="open()" [header]="'History of record ' + recordId()" (visibleChange)="!$event && closed.emit()">
    @if (loading()) {
      <pbdev-loader />
    } @else if (error()) {
      <p class="atlas-error" role="alert">{{ error() }}</p>
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
export class AtlasHistoryDialog {
  // Review NG-10: signal inputs (the bindings of the callers do not change) and one load at a time: a new record or a
  // close cancels the request before it, so a late answer for record 5 cannot show under record 7.
  readonly open = input(false);
  readonly appCode = input.required<string>();
  readonly recordId = input.required<number>();
  readonly closed = output<void>();

  private readonly api = inject(AtlasApiService);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly entries = signal<HistoryEntry[]>([]);

  private readonly request = computed(() => (this.open() && this.recordId() ? { code: this.appCode(), id: this.recordId() } : null));

  constructor() {
    toObservable(this.request)
      .pipe(
        switchMap((request) => {
          if (!request) {
            this.loading.set(false);
            return EMPTY;
          }
          this.loading.set(true);
          this.error.set(null);
          this.entries.set([]);
          return this.api.auditLog(request.code, request.id).pipe(
            catchError((error: unknown) => {
              this.error.set(problemText(error));
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((logs) => {
        if (logs) {
          this.entries.set(logs.map((x) => toEntry(x)));
        }
        this.loading.set(false);
      });
  }
}

/**
 * Review 3 RV-L19: "yyyy-MM-dd HH:mm:ss" (the time of an app with AuditTime Local) as a local Date. DatePipe does not
 * read the space form in all browsers, and an error there leaves the dialog empty. Another text is given as it is.
 */
export function localTime(text: string | null | undefined): Date | string {
  const match = /^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})(?::(\d{2}))?/.exec(text ?? '');
  return match
    ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]), Number(match[4]), Number(match[5]), Number(match[6] ?? 0))
    : (text ?? '');
}

export function toEntry(log: CPAuditLog): HistoryEntry {
  const items = Array.isArray(log.LogMessage) ? (log.LogMessage as CPAuditChange[]) : [];
  return {
    event: log.Event ?? 'Change',
    by: log.CreatedBy,
    when: log.CreatedDateUtc ? new Date(log.CreatedDateUtc) : localTime(log.CreatedDate),
    changes: items.filter((x) => x.Name !== '_Event'),
  };
}
