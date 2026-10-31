// COPY to the target: WebUi/src/app/platform/dashboard/. AG Grid column definitions from the tab Columns list
// (spec 03, 4.6). Cells are text; a link is made with DOM methods, never HTML text (H5).
import { ColDef, ICellRendererParams, ValueFormatterParams } from 'ag-grid-community';
import { CPAtlasTab, CPTabColumn } from '../atlas-models';
import { AtlasActionMenu } from './atlas-action-menu';

export interface AtlasRowActions {
  edit(row: Record<string, unknown>): void;
  history(row: Record<string, unknown>): void;
  remove(row: Record<string, unknown>): void;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** "2026-10-18" or ISO date-time to "18-Oct-2026". Other text stays. */
export function formatDate(value: unknown): string {
  if (value === null || value === undefined || value === '') {
    return '';
  }
  const text = String(value);
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(text);
  if (!match) {
    return text;
  }
  if (text.length > 10) {
    const date = new Date(text);
    if (!Number.isNaN(date.getTime())) {
      return `${String(date.getDate()).padStart(2, '0')}-${MONTHS[date.getMonth()]}-${date.getFullYear()}`;
    }
  }
  return `${match[3]}-${MONTHS[Number(match[2]) - 1]}-${match[1]}`;
}

/**
 * The local day of a cell, as formatDate shows it (review 3 RV-16): a date-time with a time part is a timestamp and
 * shows the day of the user; a yyyy-MM-dd date is that day. Before, the filter used the UTC day of a timestamp, so a
 * row shown as 17-Oct did not match a filter of 17-Oct.
 */
export function cellDay(value: unknown): Date | null {
  const text = String(value ?? '').trim();
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(text);
  if (!match) {
    return null;
  }
  if (text.length > 10) {
    const date = new Date(text);
    if (!Number.isNaN(date.getTime())) {
      return new Date(date.getFullYear(), date.getMonth(), date.getDate());
    }
  }
  return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
}

function dateComparator(filterDate: Date, cellValue: unknown): number {
  const cell = cellDay(cellValue);
  if (!cell) {
    return -1;
  }
  return cell < filterDate ? -1 : cell > filterDate ? 1 : 0;
}

/**
 * The columns of a tab. The Actions column is first, as the target GenerateColumns: headerName "Actions", width 100,
 * cellRendererParams.cpMenus = tab.Menus. actionMenu: the cell component; default AtlasActionMenu (the target menu).
 * At the target, cp-atlas-dashboard can give its own ActionMenuComponent (it calls params.context, the dashboard).
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
    filter: type === 'number' ? 'agNumberColumnFilter' : type === 'date' ? 'agDateColumnFilter' : 'agTextColumnFilter',
  };
  if (type === 'number' || type === 'currency') {
    def.type = 'numericColumn';
  }
  if (type === 'date') {
    def.valueFormatter = (p: ValueFormatterParams) => formatDate(p.value);
    def.filterParams = { comparator: dateComparator, inRangeInclusive: true };
  }
  if ((column.Link ?? '').toLowerCase() === 'edit') {
    def.cellRenderer = (params: ICellRendererParams) => {
      if (params.node.rowPinned) {
        return document.createTextNode(params.value === null || params.value === undefined ? '' : String(params.value));
      }
      const link = document.createElement('a');
      link.className = 'atlas-link';
      link.href = '#'; // review NG-08: a link with href takes the focus and opens with Enter
      link.textContent = params.value === null || params.value === undefined || params.value === '' ? '(open)' : String(params.value);
      link.addEventListener('click', (event) => {
        event.preventDefault();
        actions.edit(params.data as Record<string, unknown>);
      });
      return link;
    };
  }
  return def;
}

/** The totals row for the columns with Sum (spec 03, 4.6), or null if there is no such column. */
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
