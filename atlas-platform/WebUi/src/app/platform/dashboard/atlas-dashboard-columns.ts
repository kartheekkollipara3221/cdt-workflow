// COPY to the target: WebUi/src/app/platform/dashboard/.
// AG Grid column definitions from the Columns list of a tab.
// Cells are text. A link is made with DOM methods, never from HTML text, so a value cannot inject HTML.
import { formatDate as formatDay } from '@angular/common';
import { ColDef, ICellRendererParams, ValueFormatterParams } from 'ag-grid-community';
import { isNil, parseLocalDay } from '../atlas-dates';
import { CPAtlasTab, CPTabColumn } from '../atlas-models';
import { AtlasActionMenu } from './atlas-action-menu';

/** The actions that a dashboard row can start. */
export interface AtlasRowActions {
  edit(row: Record<string, unknown>): void;
  history(row: Record<string, unknown>): void;
  remove(row: Record<string, unknown>): void;
}

/**
 * Formats a date cell as "18-Oct-2026".
 *
 * Accepts "2026-10-18" or an ISO date-time. Returns other text unchanged, and an empty string for no value.
 */
export function formatDate(value: unknown): string {
  if (isNil(value) || value === '') {
    return '';
  }
  const text = String(value);
  const day = localDay(text);
  return day ? formatDay(day, 'dd-MMM-yyyy', 'en-US') : text;
}

const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * Formats a currency cell as "$1,250,000.00".
 *
 * Accepts a number or a numeric text. Returns other text unchanged, and an empty string for no value.
 */
export function formatCurrency(value: unknown): string {
  if (isNil(value) || value === '') {
    return '';
  }
  const amount = typeof value === 'number' ? value : Number(String(value).trim());
  return Number.isFinite(amount) ? usd.format(amount) : String(value);
}

/**
 * Returns the local day of a cell, the same day that formatDate shows.
 *
 * A date-time with a time part is a timestamp. It gives the day in the time zone of the user.
 * A yyyy-MM-dd date gives that day. The date filter uses this day, so it matches the shown value.
 * Returns null when the value is not a date.
 */
export function cellDay(value: unknown): Date | null {
  return localDay(String(value ?? '').trim());
}

function localDay(text: string): Date | null {
  const day = parseLocalDay(text);
  if (day && text.length > 10) {
    const date = new Date(text);
    if (!Number.isNaN(date.getTime())) {
      return new Date(date.getFullYear(), date.getMonth(), date.getDate());
    }
  }
  return day;
}

function dateComparator(filterDate: Date, cellValue: unknown): number {
  const cell = cellDay(cellValue);
  if (!cell) {
    return -1;
  }
  return cell < filterDate ? -1 : cell > filterDate ? 1 : 0;
}

/**
 * Returns the AG Grid columns of a tab.
 *
 * When the tab has a visible menu, the Actions column is first, as in the target GenerateColumns.
 * It has headerName "Actions", width 100 and cellRendererParams.cpMenus = tab.Menus.
 *
 * @param actions The actions that the cells start for a row.
 * @param actionMenu The cell component of the Actions column. The default is AtlasActionMenu.
 *     The target cp-atlas-dashboard can give its own ActionMenuComponent, which calls params.context.
 */
export function columnDefs(tab: CPAtlasTab, actions: AtlasRowActions, actionMenu: unknown = AtlasActionMenu): ColDef[] {
  const defs: ColDef[] = (tab.Columns ?? []).map((column) => columnDef(column, actions));
  if ((tab.Menus ?? []).some((x) => x.Visible !== false)) {
    defs.unshift({
      headerName: 'Actions',
      colId: '_actions',
      cellRenderer: actionMenu,
      sortable: false,
      filter: false,
      width: 100,
      cellRendererParams: { cpMenus: tab.Menus, atlasActions: actions },
    });
  }
  return defs;
}

function columnDef(column: CPTabColumn, actions: AtlasRowActions): ColDef {
  const type = (column.Type ?? 'string').toLowerCase();
  const def: ColDef = {
    field: column.Name,
    headerName: column.Label ?? column.Name,
    hide: !!column.Hidden,
    width: column.Width ?? undefined,
    flex: column.Width ? undefined : 1,
    minWidth: 110,
    sortable: true,
    filter: type === 'number' || type === 'currency' ? 'agNumberColumnFilter' : type === 'date' ? 'agDateColumnFilter' : 'agTextColumnFilter',
  };
  if (type === 'number' || type === 'currency') {
    def.type = 'numericColumn';
  }
  if (type === 'currency') {
    def.valueFormatter = (p: ValueFormatterParams) => formatCurrency(p.value);
  }
  if (type === 'date') {
    def.valueFormatter = (p: ValueFormatterParams) => formatDate(p.value);
    def.filterParams = { comparator: dateComparator, inRangeInclusive: true };
  }
  if ((column.Link ?? '').toLowerCase() === 'edit') {
    def.cellRenderer = (params: ICellRendererParams) => {
      if (params.node.rowPinned) {
        return document.createTextNode(isNil(params.value) ? '' : String(params.value));
      }
      const link = document.createElement('a');
      link.className = 'atlas-link';
      link.href = '#'; // A link with href can take the focus and opens with the Enter key.
      link.textContent = isNil(params.value) || params.value === '' ? '(open)' : String(params.value);
      link.addEventListener('click', (event) => {
        event.preventDefault();
        actions.edit(params.data as Record<string, unknown>);
      });
      return link;
    };
  }
  return def;
}

/**
 * Returns the totals row for the columns with Sum, or null when no column has Sum.
 *
 * The first visible column without Sum shows the text "Total".
 */
export function sumsRow(tab: CPAtlasTab, rows: Record<string, unknown>[]): Record<string, unknown> | null {
  const columns = (tab.Columns ?? []).filter((x) => x.Sum);
  if (columns.length === 0) {
    return null;
  }
  const total: Record<string, unknown> = {};
  for (const column of columns) {
    total[column.Name] = rows.reduce((sum, row) => sum + (Number(row[column.Name]) || 0), 0);
  }
  const first = (tab.Columns ?? []).find((x) => !x.Hidden && !x.Sum);
  if (first) {
    total[first.Name] = 'Total';
  }
  return total;
}
