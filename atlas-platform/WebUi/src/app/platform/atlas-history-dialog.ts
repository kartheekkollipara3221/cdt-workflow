// COPY to the target: WebUi/src/app/platform/. The audit history of one record (v1/GetAuditLog, spec 09, 4.5).
// All values are shown as text (no HTML). The layout is the target History Log (history-report-modal.ts): one block
// for each entry, "Modified On : ..." and "Modified By : ...", then one "Name - Value" line for each changed field.
import { formatDate } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, of, switchMap } from 'rxjs';
import { problemText } from './atlas-errors';
import { DialogComponent, LoaderComponent } from './atlas-pbdev';
import { AtlasApiService } from './atlas-api.service';
import { CPAuditChange, CPAuditLog } from './atlas-models';

interface HistoryEntry {
  by: string;
  when: string;
  changes: CPAuditChange[];
  /** A LogMessage that is not a JSON list (old text form), shown as it is. */
  text: string | null;
}

@Component({
  selector: 'atlas-history-dialog',
  imports: [DialogComponent, LoaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush, // review NG-11
  template: `<pbdev-dialog [visible]="open()" header="History Log" [style]="dialogStyle" (visibleChange)="!$event && closed.emit()">
    @if (loading()) {
      <pbdev-loader />
    } @else if (error()) {
      <p class="atlas-error" role="alert">{{ error() }}</p>
    } @else if (entries().length === 0) {
      <p>No history.</p>
    } @else {
      <table class="atlas-history-table">
        <tbody>
          @for (entry of entries(); track $index) {
            <tr>
              <td>
                <table>
                  <thead>
                    <tr>
                      <th>Modified On : {{ entry.when }}</th>
                      <th>Modified By : {{ entry.by }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td colspan="2">
                        @for (change of entry.changes; track $index) {
                          <div>{{ change.Name }} - {{ change.Value ?? '' }}</div>
                        }
                        @if (entry.text) {
                          <div>{{ entry.text }}</div>
                        }
                      </td>
                    </tr>
                  </tbody>
                </table>
              </td>
            </tr>
          }
        </tbody>
      </table>
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
  /** As the target modal (history-report-modal.css: width 80%, height 80vh). */
  protected readonly dialogStyle = { width: '80vw', height: '80vh' };

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
 * The changed fields of a LogMessage: JSON text (the server answer, as the target) or a JSON list. The _Event item is
 * left out. Null: not a JSON list (old text form).
 */
export function logItems(message: unknown): CPAuditChange[] | null {
  let value = message;
  if (typeof message === 'string') {
    try {
      value = JSON.parse(message);
    } catch {
      return null;
    }
  }
  return Array.isArray(value) ? (value as CPAuditChange[]).filter((x) => x?.Name !== '_Event') : null;
}

export function toEntry(log: CPAuditLog): HistoryEntry {
  const items = logItems(log.LogMessage);
  return {
    by: log.CreatedBy,
    // CreatedDate is the text of the target ("MM-dd-yyyy HH:mm:ss"); a UTC app also sends CreatedDateUtc: the user's time.
    when: log.CreatedDateUtc ? formatDate(new Date(log.CreatedDateUtc), 'MM-dd-yyyy HH:mm:ss', 'en-US') : log.CreatedDate,
    changes: items ?? [],
    text: items === null && log.LogMessage !== null && log.LogMessage !== undefined ? String(log.LogMessage) : null,
  };
}
