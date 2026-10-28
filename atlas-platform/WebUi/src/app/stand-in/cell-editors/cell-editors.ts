// STAND-IN: do not copy to the target. Local look-alikes of the target cell components
// WebUi/Source/App/Shared/Component/Cell-editors (NumericEditor, TextEditor, SelectEditor) as of 2026-10-27.
// The platform grid finds them by the cellRenderer name in the JSON (provideAtlasGridComponents); at the target the
// real components are given. Same selectors, params (disabled, code, ctrlname, options) and value flow
// (params.setValue on blur / change). Not here: the "code === 'TTD'" weekly total of the target NumericEditor
// (an app of the platform uses Computed instead).
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { ICellEditorAngularComp, ICellRendererAngularComp } from 'ag-grid-angular';

interface CellParams {
  value?: unknown;
  disabled?: boolean;
  code?: string;
  ctrlname?: string;
  type?: string;
  options?: string[];
  node: { rowIndex: number | null; data: unknown };
  setValue?: (value: unknown) => void;
  ngModelChange?: (params: { value: unknown; rowData: unknown }) => void;
}

@Component({
  selector: 'numeric-cell',
  imports: [FormsModule],
  template: `<input style="width:100%; height:98%;" [id]="id" (ngModelChange)="numericValidate($event)" (keypress)="numberOnly($event)"
    (blur)="onBlur()" (change)="onChange()" [(ngModel)]="value" [disabled]="disabled" />`,
})
export class NumericEditor implements ICellRendererAngularComp {
  private params!: CellParams;
  value = '';
  isValid = false;
  disabled = false;
  code = '';
  id = '';

  agInit(params: unknown): void {
    this.params = params as CellParams;
    this.code = this.params.code || '';
    this.id = this.params.ctrlname + '_' + this.params.node.rowIndex;
    this.disabled = this.params.disabled || false;
    this.value = this.params.value === null || this.params.value === undefined ? '' : String(this.params.value);
  }

  numericValidate(value: string): void {
    this.value = value;
    this.isValid = /^-?(0|[1-9]\d*)(\.\d+)?$/.test(value);
    this.params.ngModelChange?.({ value, rowData: this.params.node.data });
  }

  numberOnly(event: KeyboardEvent): boolean {
    const allowed = /[0-9.]/.test(event.key) || event.key.length > 1;
    if (!allowed) {
      event.preventDefault();
    }
    return allowed;
  }

  onBlur(): void {
    this.params.setValue?.(this.value);
  }

  onChange(): void {
    this.params.setValue?.(this.value);
  }

  refresh(params: unknown): boolean {
    this.agInit(params);
    return true;
  }
}

@Component({
  selector: 'text-editor-cell',
  imports: [FormsModule],
  template: `<input [type]="type" (ngModelChange)="ngModelChange($event)" [(ngModel)]="value" (blur)="onBlur()" [disabled]="disabled"
    style="width:100%; height:98%;" />`,
})
export class TextEditor implements ICellEditorAngularComp {
  private params!: CellParams;
  value = '';
  type = 'text';
  disabled = false;

  agInit(params: unknown): void {
    this.params = params as CellParams;
    this.value = this.params.value === null || this.params.value === undefined ? '' : String(this.params.value);
    this.type = this.params.type || 'text';
    this.disabled = this.params.disabled || false;
  }

  ngModelChange(value: unknown): void {
    this.params.ngModelChange?.({ value, rowData: this.params.node.data });
  }

  getValue(): unknown {
    return this.value;
  }

  onBlur(): void {
    this.params.setValue?.(this.value);
  }
}

@Component({
  selector: 'select-editor-cell',
  template: `<select (change)="onChange($event)" style="width:100%; height:100%;" [disabled]="disabled">
    @for (opt of options; track opt) {
      <option [selected]="opt === current" [value]="opt">{{ opt }}</option>
    }
  </select>`,
})
export class SelectEditor implements ICellRendererAngularComp {
  private params!: CellParams;
  options: string[] = [];
  current = '';
  disabled = false;

  agInit(params: unknown): void {
    this.params = params as CellParams;
    this.options = this.params.options ?? [];
    this.current = String(this.params.value ?? '');
    this.disabled = this.params.disabled || false;
  }

  refresh(params: unknown): boolean {
    this.agInit(params);
    return true;
  }

  onChange(event: Event): void {
    this.params.setValue?.((event.target as HTMLSelectElement).value);
  }
}
