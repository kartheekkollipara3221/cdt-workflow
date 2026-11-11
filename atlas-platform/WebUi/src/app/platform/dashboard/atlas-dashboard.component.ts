// COPY to the target as a reference: WebUi/src/app/platform/dashboard/.
// The target dashboard component gets only two changes: the navigation and the Columns support.
// The navigation calls AtlasDashboardNavigation first, then the current if chain.
// Locally this component is the dashboard of a platform app (route dashboard/:code).
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { AgGridAngular } from 'ag-grid-angular';
import { ClientSideRowModelModule, ColDef, Module, RowClassParams } from 'ag-grid-community';
import { AgLoadingOverlayComponent, DialogComponent, LoaderComponent } from '../atlas-pbdev';
import { EMPTY, Observable, Subject, catchError, startWith, switchMap, tap } from 'rxjs';
import { AtlasApiService } from '../atlas-api.service';
import { AtlasHistoryDialog } from '../atlas-history-dialog';
import { CPAtlasDashboard, CPAtlasTab, CPButton, CPButtonTarget } from '../atlas-models';
import { AtlasDashboardNavigation } from '../atlas-navigation';
import { AtlasNoticeService } from '../atlas-notice.service';
import { problemText } from '../atlas-errors';
import { columnDefs, sumsRow } from './atlas-dashboard-columns';

/** The dashboard of a platform app: the buttons, the tabs and a grid of rows for each tab. */
@Component({
  selector: 'atlas-dashboard',
  imports: [AgGridAngular, LoaderComponent, DialogComponent, AtlasHistoryDialog],
  templateUrl: './atlas-dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AtlasDashboardComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(AtlasApiService);
  private readonly navigation = inject(AtlasDashboardNavigation);
  private readonly notice = inject(AtlasNoticeService);
  private readonly destroyRef = inject(DestroyRef);

  // As cp-atlas-dashboard.ts: the modules (the app registers the others), defaultColDef and the loading overlay.
  protected readonly modules: Module[] = [ClientSideRowModelModule];
  protected readonly defaultColDef: ColDef = {
    sortable: true,
    resizable: false,
    suppressMovable: true,
    filter: 'agTextColumnFilter',
    suppressHeaderMenuButton: true,
    floatingFilter: false,
    floatingFilterComponentParams: { suppressFilterButton: true },
    filterParams: { buttons: ['apply', 'reset'] },
    unSortIcon: true,
  };
  protected readonly loadingOverlayComponent = AgLoadingOverlayComponent;
  /** The total row is at the top with the class grand-total-row (target: grandTotalRow 'top', an Enterprise feature). */
  protected readonly rowClass = (params: RowClassParams) => (params.node.rowPinned === 'top' ? 'grand-total-row' : '');
  /** The text of the search box (target onSearchChange: quick filter). */
  readonly search = signal('');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly dashboard = signal<CPAtlasDashboard | null>(null);
  readonly tabIndex = signal(0);
  /** The row whose history shows. Its app is the Code of the tab, which can differ from the dashboard app. */
  readonly historyRow = signal<AtlasRowRef | null>(null);
  readonly deleteRow = signal<AtlasRowRef | null>(null);

  readonly tabs = computed(() => (this.dashboard()?.Tabs ?? []).filter((x) => x.Visible !== false));
  readonly tab = computed<CPAtlasTab | null>(() => this.tabs()[this.tabIndex()] ?? null);
  readonly rows = computed(() => (this.tab()?.KeyValues ?? []) as Record<string, unknown>[]);
  readonly columns = computed<ColDef[]>(() => {
    const tab = this.tab();
    return tab ? columnDefs(tab, { edit: (r) => this.open(r), history: (r) => this.historyRow.set(this.rowRef(r)), remove: (r) => this.deleteRow.set(this.rowRef(r)) }) : [];
  });
  readonly totals = computed(() => {
    const tab = this.tab();
    const total = tab ? sumsRow(tab, this.rows()) : null;
    return total ? [total] : [];
  });

  code = '';

  private readonly reloads = new Subject<void>();

  ngOnInit(): void {
    // A new route or a reload cancels the load before it, so a late answer cannot show the rows of another app.
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          this.code = (params.get('code') ?? '').toUpperCase();
          this.tabIndex.set(0);
          this.search.set('');
          return this.reloads.pipe(
            startWith(undefined),
            switchMap(() => this.fetch()),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  /** Loads the dashboard of the current app again (after a delete). */
  load(): void {
    this.reloads.next();
  }

  private fetch(): Observable<CPAtlasDashboard> {
    this.loading.set(true);
    this.error.set(null);
    return this.api.dashboard(this.code).pipe(
      tap((dashboard) => {
        this.dashboard.set(dashboard);
        if (this.tabIndex() >= this.tabs().length) {
          this.tabIndex.set(0);
        }
        this.loading.set(false);
      }),
      catchError((error) => {
        this.error.set(problemText(error));
        this.loading.set(false);
        return EMPTY;
      }),
    );
  }

  readonly visibleButtons = computed<CPButton[]>(() => (this.dashboard()?.Buttons ?? []).filter((x) => x.Visible));

  /**
   * Opens the form for a header button.
   *
   * Uses the Target of the button when it has one. Otherwise opens a new record of the dashboard app.
   */
  onButton(button: CPButton): void {
    this.navigate(this.code, 0, button.Target ?? { AppCode: this.code, Id: 0 });
  }

  /** Selects a tab and clears the search text. */
  selectTab(index: number): void {
    this.tabIndex.set(index);
    this.search.set('');
  }

  /** Sets the search text from the search box. */
  onSearchChange(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  /**
   * Moves between the tabs with the keyboard, as in the WAI-ARIA tabs pattern.
   *
   * The arrow keys, Home and End select a tab. The new tab gets the focus.
   */
  onTabKey(event: KeyboardEvent): void {
    const count = this.tabs().length;
    const current = this.tabIndex();
    const next =
      event.key === 'ArrowRight' ? (current + 1) % count
      : event.key === 'ArrowLeft' ? (current - 1 + count) % count
      : event.key === 'Home' ? 0
      : event.key === 'End' ? count - 1
      : -1;
    if (count === 0 || next < 0) {
      return;
    }
    event.preventDefault();
    this.selectTab(next);
    const id = this.tabs()[next]?.ID;
    queueMicrotask(() => document.getElementById('tab_' + id)?.focus());
  }

  /** Opens the form of a row. */
  open(row: Record<string, unknown>): void {
    const ref = this.rowRef(row);
    this.navigate(ref.code, ref.id);
  }

  /** Returns the app and the ID of a row of the current tab. */
  rowRef(row: Record<string, unknown>): AtlasRowRef {
    return { code: this.tab()?.Code || this.code, id: id(row) };
  }

  /** Deletes the row of the open delete dialog, then shows the result and loads the dashboard again. */
  confirmRemove(): void {
    const ref = this.deleteRow();
    this.deleteRow.set(null);
    if (!ref) {
      return;
    }
    const recordId = ref.id;
    this.api.remove(ref.code, recordId).subscribe({
      next: (result) => {
        if (result.Errors?.length) {
          this.notice.show('error', result.Errors.map((x) => x.Message).join(' '));
          return;
        }
        this.notice.show('success', 'Record deleted successfully');
        this.load();
      },
      error: (error) => this.notice.show('error', problemText(error)),
    });
  }

  /**
   * Opens the FormType route of the app through AtlasDashboardNavigation, as at the target.
   *
   * When the app has no FormType, opens the platform form.
   */
  private async navigate(code: string, recordId: number, target?: CPButtonTarget): Promise<void> {
    if (!(await this.navigation.open(code, recordId, target))) {
      // At the target: with no FormType, the current if chain runs here.
      await this.router.navigate(['/form', target?.AppCode ?? code, target ? (target.Id ?? 0) : recordId]);
    }
  }
}

/** The app code and the record ID of a dashboard row. */
export interface AtlasRowRef {
  code: string;
  id: number;
}

function id(row: Record<string, unknown>): number {
  return Number(row['ID'] ?? row['Id'] ?? 0);
}
