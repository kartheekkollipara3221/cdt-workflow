// COPY to the target as a reference: WebUi/Source/App/platform/dashboard/. At the target, the current dashboard
// component gets only the navigation change (formRoute first, then the current if chain) and the Columns support.
// Locally this component is the dashboard of a platform app (route dashboard/:code).
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { AgGridAngular } from 'ag-grid-angular';
import { ClientSideRowModelModule, ColDef, Module, RowClassParams } from 'ag-grid-community';
import { AgLoadingOverlayComponent, DialogComponent, LoaderComponent } from '../atlas-pbdev';
import { forkJoin } from 'rxjs';
import { AtlasApiService } from '../atlas-api.service';
import { AtlasHistoryDialog } from '../atlas-history-dialog';
import { CPApplicationRow, CPAtlasDashboard, CPAtlasTab, CPButton } from '../atlas-models';
import { formRoute, formTypeOf } from '../atlas-navigation';
import { AtlasNoticeService } from '../atlas-notice.service';
import { problemText } from '../atlas-errors';
import { columnDefs, sumsRow } from './atlas-dashboard-columns';

@Component({
  selector: 'atlas-dashboard',
  imports: [AgGridAngular, LoaderComponent, DialogComponent, AtlasHistoryDialog],
  templateUrl: './atlas-dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush, // review NG-11
})
export class AtlasDashboardComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(AtlasApiService);
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
  /** Review NG-07: the app of the row is the tab Code (spec 03), not always the dashboard app. */
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
  private apps: CPApplicationRow[] = [];

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.code = (params.get('code') ?? '').toUpperCase();
      this.tabIndex.set(0);
      this.search.set('');
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({ apps: this.api.applications(), dashboard: this.api.dashboard(this.code) }).subscribe({
      next: ({ apps, dashboard }) => {
        this.apps = apps;
        this.dashboard.set(dashboard);
        if (this.tabIndex() >= this.tabs().length) {
          this.tabIndex.set(0);
        }
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(problemText(error));
        this.loading.set(false);
      },
    });
  }

  visibleButtons(): CPButton[] {
    return (this.dashboard()?.Buttons ?? []).filter((x) => x.Visible);
  }

  /** The add button: Target first (spec 03, 4.5). */
  onButton(button: CPButton): void {
    const target = button.Target ?? { AppCode: this.code, Id: 0 };
    this.navigate(target.AppCode, target.Id);
  }

  selectTab(index: number): void {
    this.tabIndex.set(index);
    this.search.set('');
  }

  onSearchChange(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  /** Review NG-08 (WAI-ARIA tabs): arrow keys, Home and End move between the tabs; the new tab gets the focus. */
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

  open(row: Record<string, unknown>): void {
    const ref = this.rowRef(row);
    this.navigate(ref.code, ref.id);
  }

  /** The app and the ID of a row of the current tab. */
  rowRef(row: Record<string, unknown>): AtlasRowRef {
    return { code: this.tab()?.Code || this.code, id: id(row) };
  }

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

  private navigate(code: string, recordId: number): void {
    const route = formRoute(formTypeOf(this.apps, code), code, recordId);
    // At the target: with no FormType, the current if chain runs here.
    this.router.navigate(route ?? ['/form', code, recordId]);
  }
}

export interface AtlasRowRef {
  code: string;
  id: number;
}

function id(row: Record<string, unknown>): number {
  return Number(row['ID'] ?? row['Id'] ?? 0);
}
