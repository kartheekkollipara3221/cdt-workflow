// COPY to the target: WebUi/src/app/platform/form/renderers/. The "grid" control (review R4), as the grid of
// cp-atlas-sectionmaster.html (2026-10-27): ag-grid-angular with class ag-theme-pbdev, [modules] ClientSideRowModelModule,
// domLayout autoHeight, the same defaultColDef, AgLoadingOverlayComponent, and the cell components named by
// "cellRenderer" in the JSON (NumericEditor, TextEditor, SelectEditor at the target), given with
// provideAtlasGridComponents (atlas-grid-components.ts). BindMode "SingleRowFields" (spec 01, 10.2): one row; each column is a field of the record.
// The form keeps a form group with one control for each column field (validation, Computed, server errors); the grid
// row and that group are kept the same in both directions.
// HeaderFromDate (platform): the header shows the date of a form control + AddDays, for example the 7 days before
// WeekEnding, as the TTD form at the target. It changes when the control value changes.
import { formatDate } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject, isDevMode } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormGroup } from '@angular/forms';
import { AgGridAngular } from 'ag-grid-angular';
import { CellValueChangedEvent, ClientSideRowModelModule, ColDef, GridApi, GridReadyEvent, Module, RowSelectionOptions } from 'ag-grid-community';
import { AgLoadingOverlayComponent } from '../../atlas-pbdev';
import { CPGridColumn, CPHeaderFromDate } from '../../atlas-models';
import { ATLAS_GRID_COMPONENTS } from '../atlas-grid-components';
import { errorText } from '../atlas-validators';
import { AtlasRendererBase } from './atlas-renderers';

@Component({
  selector: 'atlas-grid-renderer',
  imports: [AgGridAngular],
  template: `@if (rowGroup) {
    <ag-grid-angular class="ag-theme-pbdev" [modules]="modules" domLayout="autoHeight" [rowSelection]="rowSelection"
      [defaultColDef]="defaultColDef" [suppressContextMenu]="true" [suppressCellFocus]="true" [suppressColumnVirtualisation]="true"
      [pagination]="!!control.Grid?.Paginate" [loadingOverlayComponent]="loadingOverlayComponent" [rowData]="rowData"
      [columnDefs]="columns" [components]="components" (gridReady)="onGridReady($event)" (cellValueChanged)="onCellValueChanged($event)" />
    @for (message of cellErrors; track message) {
      <div class="atlas-error" role="alert">{{ message }}</div>
    }
  } @else {
    <div class="atlas-unsupported" role="note">The grid "{{ control.Name }}" needs Grid.BindMode "SingleRowFields".</div>
  }`,
})
export class AtlasGridRenderer extends AtlasRendererBase implements OnInit {
  readonly components = inject(ATLAS_GRID_COMPONENTS);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  // As cp-atlas-sectionmaster.ts (grid part).
  protected readonly modules: Module[] = [ClientSideRowModelModule];
  protected readonly defaultColDef: ColDef = {
    flex: 1,
    sortable: true,
    resizable: false,
    suppressMovable: true,
    filter: 'agTextColumnFilter',
    suppressHeaderMenuButton: true,
    floatingFilter: false,
    floatingFilterComponentParams: { suppressFilterButton: true },
    filterParams: {},
    unSortIcon: true,
  };
  /** The target writes rowSelection="single" (deprecated in ag-grid 33); this is the same setting. */
  protected readonly rowSelection: RowSelectionOptions = { mode: 'singleRow', checkboxes: false, enableClickSelection: true };
  protected readonly loadingOverlayComponent = AgLoadingOverlayComponent;

  rowData: Record<string, unknown>[] = [];
  columns: ColDef[] = [];
  private api: GridApi | null = null;

  get rowGroup(): FormGroup | null {
    const item = this.group.get(this.control.Name);
    return item instanceof FormGroup ? item : null;
  }

  ngOnInit(): void {
    const rowGroup = this.rowGroup;
    if (!rowGroup) {
      return;
    }
    this.rowData = [{ ...rowGroup.getRawValue() }];
    this.columns = (this.control.Grid?.Columns ?? []).map((column) => this.columnDef(column, rowGroup));
    // A Computed cell (for example WeeklyTotal) or a server answer changes the group: show it in the grid row.
    this.group.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.syncGrid();
      this.syncHeaders();
    });
  }

  onGridReady(event: GridReadyEvent): void {
    this.api = event.api;
    this.syncGrid();
  }

  /** A cell component calls params.setValue: the value goes to the form control of the field. */
  onCellValueChanged(event: CellValueChangedEvent): void {
    const field = event.colDef.field;
    const cell = field ? this.rowGroup?.get(field) : null;
    if (!cell) {
      return;
    }
    const value = event.newValue === null || event.newValue === undefined ? '' : String(event.newValue);
    if (String(cell.value ?? '') !== value) {
      cell.setValue(value);
    }
    cell.markAsTouched();
    this.cdr.markForCheck();
  }

  /** The errors of the cells, under the grid (the cell components show no error). */
  get cellErrors(): string[] {
    const rowGroup = this.rowGroup;
    if (!rowGroup) {
      return [];
    }
    const messages: string[] = [];
    for (const column of this.control.Grid?.Columns ?? []) {
      const cell = column.field ? rowGroup.get(column.field) : null;
      if (cell && cell.enabled && cell.invalid && (cell.touched || cell.errors?.['server'])) {
        const label = column.headerName ?? column.field!;
        messages.push(`${label}: ${errorText(cell.errors, label, this.control.Validator)}`);
      }
    }
    return messages;
  }

  private columnDef(column: CPGridColumn, rowGroup: FormGroup): ColDef {
    const def = { ...column } as ColDef & Record<string, unknown>;
    const cell = column.field ? rowGroup.get(column.field) : null;
    const name = typeof column.cellRenderer === 'string' ? column.cellRenderer : null;
    delete def['HeaderFromDate'];
    def.headerName = this.headerName(column);
    if (name && !this.components[name]) {
      if (isDevMode()) {
        console.warn(`Atlas: no grid component "${name}" for the column ${column.field}. Add it with provideAtlasGridComponents.`);
      }
      delete def['cellRenderer'];
    }
    def.cellRendererParams = { ...(column.cellRendererParams ?? {}), disabled: !!column.cellRendererParams?.disabled || !cell || cell.disabled };
    return def;
  }

  private headerName(column: CPGridColumn): string | undefined {
    const from = column.HeaderFromDate;
    const date = from ? dateHeader(from, this.group.get(from.Control)?.value) : null;
    return date ?? column.headerName ?? undefined;
  }

  /** A new date (for example another WeekEnding): new column definitions with the new headers. */
  private syncHeaders(): void {
    if (!this.columns.some((def, i) => def.headerName !== this.headerName(this.control.Grid!.Columns![i]))) {
      return;
    }
    this.columns = this.columns.map((def, i) => ({ ...def, headerName: this.headerName(this.control.Grid!.Columns![i]) }));
    this.cdr.markForCheck();
  }

  private syncGrid(): void {
    const rowGroup = this.rowGroup;
    const node = this.api?.getDisplayedRowAtIndex(0);
    if (!rowGroup || !node) {
      return;
    }
    for (const [field, value] of Object.entries(rowGroup.getRawValue() as Record<string, unknown>)) {
      if (String(node.data?.[field] ?? '') !== String(value ?? '')) {
        node.setDataValue(field, value);
      }
    }
    this.cdr.markForCheck();
  }
}

/** The header text for a date value "yyyy-MM-dd" (a local day; not UTC, so the day is the same in each time zone). Null: no date. */
export function dateHeader(from: CPHeaderFromDate, value: unknown): string | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value ?? ''));
  if (!match) {
    return null;
  }
  const day = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]) + (from.AddDays ?? 0));
  return formatDate(day, from.Format || 'EEEE dd-MMM-yyyy', 'en-US');
}
