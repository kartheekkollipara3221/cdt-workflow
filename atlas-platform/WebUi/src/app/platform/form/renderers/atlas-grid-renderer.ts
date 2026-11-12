// COPY to the target: WebUi/src/app/platform/form/renderers/.
import { formatDate } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject, isDevMode } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormGroup } from '@angular/forms';
import { AgGridAngular } from 'ag-grid-angular';
import { CellValueChangedEvent, ClientSideRowModelModule, ColDef, GridApi, GridReadyEvent, Module, RowSelectionOptions } from 'ag-grid-community';
import { isNil, parseLocalDay } from '../../atlas-dates';
import { AgLoadingOverlayComponent } from '../../atlas-pbdev';
import { CPGridColumn, CPHeaderFromDate } from '../../atlas-models';
import { ATLAS_GRID_COMPONENTS } from '../atlas-grid-components';
import { errorText } from '../atlas-validators';
import { AtlasRendererBase } from './atlas-renderers';

/**
 * The "grid" control. It is the same as the grid of the target cp-atlas-sectionmaster.html.
 *
 * It uses ag-grid-angular with the class ag-theme-pbdev, [modules] ClientSideRowModelModule, domLayout autoHeight,
 * the same defaultColDef and AgLoadingOverlayComponent. The JSON names the cell components with "cellRenderer"
 * (NumericEditor, TextEditor, SelectEditor at the target). provideAtlasGridComponents (atlas-grid-components.ts)
 * gives them.
 *
 * BindMode "SingleRowFields": the grid has one row, and each column is a field of the record.
 * The form keeps a form group with one control for each column field, for validation, Computed values and
 * server errors. The grid row and that group stay the same in both directions.
 *
 * HeaderFromDate (a platform setting): the header shows the date of a form control plus AddDays.
 * An example is the 7 days before WeekEnding, as in the TTD form at the target.
 * The header changes when the control value changes.
 */
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

  // The same settings as the grid part of cp-atlas-sectionmaster.ts.
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
    // The browser moves the focus with Tab, so Tab goes to the next cell input and, after the last cell, out of the
    // grid. The cell input loses the focus, so the cell component sends its value.
    suppressKeyboardEvent: (params) => params.event.key === 'Tab',
  };
  /** The row selection. The target writes rowSelection="single" (deprecated in ag-grid 33). This is the same setting. */
  protected readonly rowSelection: RowSelectionOptions = { mode: 'singleRow', checkboxes: false, enableClickSelection: true };
  protected readonly loadingOverlayComponent = AgLoadingOverlayComponent;

  rowData: Record<string, unknown>[] = [];
  columns: ColDef[] = [];
  private api: GridApi | null = null;

  /** The form group of the grid row, or null when the grid is not a SingleRowFields grid. */
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
    // A Computed cell (for example WeeklyTotal), an effect or a server response can change the group.
    // The grid copies the values one time, after the change is complete. Thus an effect that clears many cells
    // and the Computed values that follow give one copy, and the order of the listeners has no effect.
    this.group.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.scheduleSync());
  }

  private syncPending = false;

  /** Copies the form values and the header dates to the grid one time, after the current change. */
  private scheduleSync(): void {
    if (this.syncPending) {
      return;
    }
    this.syncPending = true;
    queueMicrotask(() => {
      this.syncPending = false;
      if (this.api?.isDestroyed()) {
        return;
      }
      this.syncGrid();
      this.syncHeaders();
    });
  }

  /** Keeps the grid API and copies the form values to the grid row. */
  onGridReady(event: GridReadyEvent): void {
    this.api = event.api;
    this.syncGrid();
  }

  /**
   * Copies a new cell value to the form control of the field and marks it as touched.
   *
   * A cell component calls params.setValue, and then the grid sends this event.
   */
  onCellValueChanged(event: CellValueChangedEvent): void {
    const field = event.colDef.field;
    const cell = field ? this.rowGroup?.get(field) : null;
    if (!cell) {
      return;
    }
    const value = isNil(event.newValue) ? '' : String(event.newValue);
    if (String(cell.value ?? '') !== value) {
      cell.setValue(value);
    }
    cell.markAsTouched();
    this.cdr.markForCheck();
  }

  /** The errors of the cells. They show under the grid, because the cell components show no error. */
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
    def.cellRendererParams = {
      ...(column.cellRendererParams ?? {}),
      disabled: !!column.cellRendererParams?.disabled || !cell || cell.disabled,
      // The cell components call ngModelChange for each key. Thus a Computed cell changes while the user types.
      ngModelChange: (change: { value?: unknown }) => column.field && this.onCellInput(column.field, change?.value),
    };
    return def;
  }

  /**
   * Copies the value that the user types in a cell to the form control of the field.
   *
   * The grid row gets the same value first. Thus the grid does not draw the cell again, and the input keeps the focus.
   */
  onCellInput(field: string, value: unknown): void {
    const cell = this.rowGroup?.get(field);
    if (!cell) {
      return;
    }
    const text = isNil(value) ? '' : String(value);
    const data = this.api?.getDisplayedRowAtIndex(0)?.data as Record<string, unknown> | undefined;
    if (data) {
      data[field] = text;
    }
    if (String(cell.value ?? '') !== text) {
      cell.setValue(text);
    }
    cell.markAsTouched();
    this.cdr.markForCheck();
  }

  private headerName(column: CPGridColumn): string | undefined {
    const from = column.HeaderFromDate;
    const date = from ? dateHeader(from, this.group.get(from.Control)?.value) : null;
    return date ?? column.headerName ?? undefined;
  }

  /** Makes new column definitions when a header date changes, for example for another WeekEnding. */
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
    let changed = false;
    for (const [field, value] of Object.entries(rowGroup.getRawValue() as Record<string, unknown>)) {
      if (String(node.data?.[field] ?? '') !== String(value ?? '')) {
        node.setDataValue(field, value);
        changed = true;
      }
    }
    if (changed) {
      // The grid gives the new value to the refresh method of the cell component. The component shows it only after
      // a change detection, and with zone.js and an OnPush parent no change detection can come. Thus check the cells now.
      this.cdr.detectChanges();
    }
    this.cdr.markForCheck();
  }
}

/**
 * Returns the header text for a date value "yyyy-MM-dd" plus AddDays, or null when there is no date.
 *
 * The date is a local day, not UTC. Thus the day is the same in each time zone.
 */
export function dateHeader(from: CPHeaderFromDate, value: unknown): string | null {
  const day = parseLocalDay(value, from.AddDays ?? 0);
  return day && formatDate(day, from.Format || 'EEEE dd-MMM-yyyy', 'en-US');
}
