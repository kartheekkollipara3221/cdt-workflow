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
      @if (show('Edit')) {
        <button mat-menu-item (click)="run('Edit')">
          <i class="fa-solid fa-pen-to-square" aria-hidden="true"></i>
          <span> {{ label('Edit') }}</span>
        </button>
      }
      @if (show('Delete')) {
        <button mat-menu-item (click)="run('Delete')">
          <i class="fa fa-trash" aria-hidden="true"></i>
          <span> {{ label('Delete') }}</span>
        </button>
      }
      @if (show('History')) {
        <button mat-menu-item (click)="run('History')">
          <i class="fa fa-history" aria-hidden="true"></i>
          <span> {{ label('History') }}</span>
        </button>
      }
    </mat-menu>
  }`,
})
export class AtlasActionMenu implements ICellRendererAngularComp {
  private params!: AtlasActionMenuParams;
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
    const row = this.params.data as Record<string, unknown>;
    const actions = this.params.atlasActions;
    if (name === 'Edit') actions.edit(row);
    else if (name === 'Delete') actions.remove(row);
    else if (name === 'History') actions.history(row);
  }

  private menu(name: string): CPMenu | undefined {
    return (this.params.cpMenus ?? []).find((x) => (x.Name ?? '').toLowerCase() === name.toLowerCase());
  }
}
