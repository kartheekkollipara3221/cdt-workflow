// COPY to the target: WebUi/Source/App/platform/form/renderers/. One small component for each control Type.
// Each renderer gets the control (JSON) and the form group, and binds the form control by name. The label, the
// required mark and the error text are drawn by the form component, so a renderer has only the input.
import { Component, Directive, Input } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { NgSelectComponent } from '@ng-select/ng-select';
import {
  AtlasControlIdDirective,
  CheckboxComponent,
  DatePickerComponent,
  InputComponent,
  SelectComponent,
  SelectOption,
  TextareaComponent,
  toSelectOptions,
} from '../../atlas-pbdev';
import { CPApplicationControl, CPGridColumn, CPKeyValue } from '../../atlas-models';

@Directive()
abstract class AtlasRenderer {
  @Input({ required: true }) control!: CPApplicationControl;
  @Input({ required: true }) group!: FormGroup;

  get formControl(): FormControl {
    return this.group.get(this.control.Name) as FormControl;
  }
}

@Component({
  selector: 'atlas-text-renderer',
  imports: [ReactiveFormsModule, InputComponent],
  template: `<pbdev-input inputType="string" [inputId]="'ctl_' + control.Name" [placeholder]="control.PlaceHolder ?? ''" [formControl]="formControl" />`,
})
export class AtlasTextRenderer extends AtlasRenderer {}

@Component({
  selector: 'atlas-select-renderer',
  imports: [ReactiveFormsModule, SelectComponent, AtlasControlIdDirective],
  template: `<pbdev-select [atlasControlId]="'ctl_' + control.Name" [options]="options" [formControl]="formControl" />`,
})
export class AtlasSelectRenderer extends AtlasRenderer {
  private source: CPKeyValue[] | null | undefined;
  private mapped: SelectOption[] = [];

  /** pbdev-select takes label/value objects; the list is mapped again only when the control gets new options. */
  get options(): SelectOption[] {
    if (this.control.Options !== this.source) {
      this.source = this.control.Options;
      this.mapped = toSelectOptions(this.source);
    }
    return this.mapped;
  }
}

/** "ngselect": a select with search, as the current component (ng-select, bindLabel/bindValue). */
@Component({
  selector: 'atlas-ngselect-renderer',
  imports: [ReactiveFormsModule, NgSelectComponent],
  template: `<ng-select [items]="control.Options ?? []" bindLabel="Label" bindValue="Value" [multiple]="!!control.Multiple"
    [clearable]="true" [virtualScroll]="true" [placeholder]="control.PlaceHolder ?? ''" [formControl]="formControl"
    [labelForId]="'ctl_' + control.Name" />`,
})
export class AtlasNgSelectRenderer extends AtlasRenderer {}

@Component({
  selector: 'atlas-textarea-renderer',
  imports: [ReactiveFormsModule, TextareaComponent, AtlasControlIdDirective],
  template: `<pbdev-textarea [atlasControlId]="'ctl_' + control.Name" [rows]="control.Row ?? 3" [formControl]="formControl" />`,
})
export class AtlasTextareaRenderer extends AtlasRenderer {}

@Component({
  selector: 'atlas-date-renderer',
  imports: [ReactiveFormsModule, DatePickerComponent, AtlasControlIdDirective],
  template: `<pbdev-date-picker [atlasControlId]="'ctl_' + control.Name" [formControl]="formControl" />`,
})
export class AtlasDateRenderer extends AtlasRenderer {}

@Component({
  selector: 'atlas-checkbox-renderer',
  imports: [ReactiveFormsModule, CheckboxComponent, AtlasControlIdDirective],
  template: `<pbdev-checkbox [atlasControlId]="'ctl_' + control.Name" inputLabel="" [formControl]="formControl" />`,
})
export class AtlasCheckboxRenderer extends AtlasRenderer {}

/** "user": a LAN ID shown as the display name (the option that the server adds). Read-only. */
@Component({
  selector: 'atlas-user-renderer',
  template: `<div class="atlas-readonly" [attr.title]="formControl.value || null">{{ displayName }}</div>`,
})
export class AtlasUserRenderer extends AtlasRenderer {
  get displayName(): string {
    const value = this.formControl.value as string | null;
    if (!value) {
      return '—';
    }
    return this.control.Options?.find((x) => x.Value === value)?.Label ?? value;
  }
}

/**
 * Grid with BindMode "SingleRowFields" (spec 01, 10.2): one row; each column is a field of the record.
 * The form group of the grid has one control for each column field.
 */
@Component({
  selector: 'atlas-grid-row-renderer',
  imports: [ReactiveFormsModule],
  template: `<div class="atlas-grid-row" [formGroup]="rowGroup">
    @for (column of columns; track column.field) {
      <label class="atlas-grid-cell" [style.min-width.px]="(column.width ?? 120) * 0.7">
        <span class="atlas-grid-header">{{ column.headerName }}</span>
        <input class="pbdev-control atlas-number" inputmode="numeric" [formControlName]="column.field!" [attr.id]="'ctl_' + column.field" />
        @if (rowGroup.get(column.field!)?.invalid && rowGroup.get(column.field!)?.touched) {
          <span class="atlas-error">{{ cellError(column) }}</span>
        }
      </label>
    }
  </div>`,
})
export class AtlasGridRowRenderer extends AtlasRenderer {
  get rowGroup(): FormGroup {
    return this.group.get(this.control.Name) as FormGroup;
  }

  get columns(): CPGridColumn[] {
    return (this.control.Grid?.Columns ?? []).filter((x) => !!x.field);
  }

  cellError(column: CPGridColumn): string {
    const errors = this.rowGroup.get(column.field!)?.errors ?? {};
    return errors['server'] ?? this.control.Validator?.Message ?? 'Not valid.';
  }
}
