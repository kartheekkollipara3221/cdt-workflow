// COPY to the target: WebUi/src/app/platform/dashboard/. The Actions cell of a platform tab: the same menu as the target
// Shared/Component/Control/Action-menu.ts (a vertical ellipsis button; a mat-menu with Edit, Delete, History and the
// same Font Awesome icons). At the target the dashboard can give its own ActionMenuComponent to columnDefs instead
// (INTEGRATION.md, cp-atlas-dashboard edit 4); this one calls the AtlasRowActions of the column, not params.context.
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

export interface AtlasActionMenuParams extends ICellRendererParams {
  cpMenus?: CPMenu[] | null;
  atlasActions: AtlasRowActions;
}

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

  /** As the target menu, from tab.Menus: an item shows when the tab has it and it is not Visible = false. */
  show(name: string): boolean {
    const menu = this.menu(name);
    return !!menu && menu.Visible !== false;
  }

  label(name: string): string {
    return this.menu(name)?.Label ?? name;
  }

  run(name: string): void {
    ITEMS.find((x) => x.name === name)?.run(this.params.atlasActions, this.params.data as Record<string, unknown>);
  }

  private menu(name: string): CPMenu | undefined {
    return (this.params.cpMenus ?? []).find((x) => (x.Name ?? '').toLowerCase() === name.toLowerCase());
  }
}
