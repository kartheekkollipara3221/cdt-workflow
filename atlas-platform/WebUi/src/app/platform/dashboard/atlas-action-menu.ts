// COPY to the target: WebUi/src/app/platform/dashboard/.
// The Actions cell of a platform tab. It looks the same as the target Shared/Component/Control/Action-menu.ts:
// a vertical ellipsis button and a mat-menu with Edit, Delete and History, with the same Font Awesome icons.
// The target dashboard can give its own ActionMenuComponent to columnDefs instead.
// This menu calls the AtlasRowActions of the column, not params.context.
import { Component } from '@angular/core';
import { MatMenuModule } from '@angular/material/menu';
import { ICellRendererAngularComp } from 'ag-grid-angular';
import { ICellRendererParams } from 'ag-grid-community';
import { CPMenu } from '../atlas-models';
import type { AtlasRowActions } from './atlas-dashboard-columns';

interface AtlasActionItem {
  name: string;
  icon: string;
  run: (actions: AtlasRowActions, row: Record<string, unknown>) => void;
}

/** The menu items, in the order of the target menu. */
const ITEMS: AtlasActionItem[] = [
  { name: 'Edit', icon: 'fa-solid fa-pen-to-square', run: (actions, row) => actions.edit(row) },
  { name: 'Delete', icon: 'fa fa-trash', run: (actions, row) => actions.remove(row) },
  { name: 'History', icon: 'fa fa-history', run: (actions, row) => actions.history(row) },
];

/** The cell renderer parameters of the Actions column. */
export interface AtlasActionMenuParams extends ICellRendererParams {
  cpMenus?: CPMenu[] | null;
  atlasActions: AtlasRowActions;
}

/** The Actions cell of a dashboard row. Pinned rows (the totals row) show no menu. */
@Component({
  selector: 'atlas-action-menu',
  imports: [MatMenuModule],
  template: `@if (!pinned) {
    <button type="button" class="atlas-action-menu" [matMenuTriggerFor]="menu" aria-label="Actions">
      <i class="fa-solid fa-ellipsis-vertical"></i>
    </button>
    <mat-menu #menu="matMenu">
      @for (item of items; track item.name) {
        @if (show(item.name)) {
          <button mat-menu-item (click)="run(item.name)">
            <i [class]="item.icon" aria-hidden="true"></i>
            <span> {{ label(item.name) }}</span>
          </button>
        }
      }
    </mat-menu>
  }`,
})
export class AtlasActionMenu implements ICellRendererAngularComp {
  private params!: AtlasActionMenuParams;
  readonly items = ITEMS;
  pinned = false;

  agInit(params: AtlasActionMenuParams): void {
    this.params = params;
    this.pinned = !!params.node.rowPinned;
  }

  refresh(): boolean {
    return false;
  }

  /**
   * Returns true when the menu item shows.
   *
   * An item shows when tab.Menus has it and its Visible is not false, as in the target menu.
   */
  show(name: string): boolean {
    const menu = this.menu(name);
    return !!menu && menu.Visible !== false;
  }

  /** Returns the label of a menu item from tab.Menus, or the item name when there is no label. */
  label(name: string): string {
    return this.menu(name)?.Label ?? name;
  }

  /** Runs the row action of a menu item for the row of this cell. */
  run(name: string): void {
    ITEMS.find((x) => x.name === name)?.run(this.params.atlasActions, this.params.data as Record<string, unknown>);
  }

  private menu(name: string): CPMenu | undefined {
    return (this.params.cpMenus ?? []).find((x) => (x.Name ?? '').toLowerCase() === name.toLowerCase());
  }
}
