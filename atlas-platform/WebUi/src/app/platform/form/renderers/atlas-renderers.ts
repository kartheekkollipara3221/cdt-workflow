// COPY to the target: WebUi/Source/App/platform/form/renderers/. One small component for each control Type.
// Each renderer gets the control (JSON) and the form group, and binds the form control by name. The label and the
// required mark are drawn by the form component (as cp-atlas-sectionmaster.html). The error text of pbdev-input,
// pbdev-select, pbdev-textarea and pbdev-date-picker is drawn by the control itself (pbdev-input-messages), with
// the platform texts given as customErrorMessages (review R2); the form draws it for the other types.
import { Component, Directive, Input, OnInit, isDevMode } from '@angular/core';
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
import { errorText } from '../atlas-validators';

/**
 * The base class of a renderer (review NG-04). A renderer of an app extends it and is added with
 * provideAtlasRenderers({ myType: MyRenderer }) or AtlasRendererRegistry.register. The form gives the two inputs.
 */
@Directive()
export abstract class AtlasRendererBase {
  @Input({ required: true }) control!: CPApplicationControl;
  @Input({ required: true }) group!: FormGroup;

  get formControl(): FormControl {
    return this.group.get(this.control.Name) as FormControl;
  }

  private messagesFor: unknown = null;
  private messages: Record<string, string> = {};

  /** customErrorMessages for a pbdev control: one platform text for each error key (same texts as the server). */
  get errorMessages(): Record<string, string> {
    const errors = this.formControl?.errors ?? null;
    if (errors !== this.messagesFor) {
      this.messagesFor = errors;
      this.messages = {};
      for (const [key, value] of Object.entries(errors ?? {})) {
        this.messages[key] = errorText({ [key]: value }, this.control.Label ?? this.control.Name, this.control.Validator) ?? 'Error';
      }
    }
    return this.messages;
  }
}

@Component({
  selector: 'atlas-text-renderer',
  imports: [ReactiveFormsModule, InputComponent, AtlasControlIdDirective],
  template: `<pbdev-input inputType="string" [atlasControlId]="'ctl_' + control.Name" [placeholder]="control.PlaceHolder ?? ''"
    [customErrorMessages]="errorMessages" [formControl]="formControl" />`,
})
export class AtlasTextRenderer extends AtlasRendererBase {}

/**
 * "currency" and "percent": as cp-atlas-sectionmaster.html, a text input with no mask (decision D1: the target's
 * maskF attribute is not a pbdev input) that clears a value of only the currency sign.
 */
@Component({
  selector: 'atlas-amount-renderer',
  imports: [ReactiveFormsModule, InputComponent, AtlasControlIdDirective],
  template: `<pbdev-input inputType="text" [atlasControlId]="'ctl_' + control.Name" [placeholder]="control.PlaceHolder ?? ''"
    [customErrorMessages]="errorMessages" [formControl]="formControl" (input)="onInput($event)" />`,
})
export class AtlasAmountRenderer extends AtlasRendererBase {
  onInput(event: Event): void {
    if ((event.target as HTMLInputElement | null)?.value === '$ ') {
      this.formControl.setValue('');
      this.formControl.updateValueAndValidity();
    }
  }
}

@Component({
  selector: 'atlas-select-renderer',
  imports: [ReactiveFormsModule, SelectComponent, AtlasControlIdDirective],
  template: `<pbdev-select [atlasControlId]="'ctl_' + control.Name" [options]="options" [customErrorMessages]="errorMessages"
    [formControl]="formControl" />`,
})
export class AtlasSelectRenderer extends AtlasRendererBase {
  private source: CPKeyValue[] | null | undefined;
  private mapped: SelectOption<string>[] = [];

  /** pbdev-select takes label/value objects; the list is mapped again only when the control gets new options. */
  get options(): SelectOption<string>[] {
    if (this.control.Options !== this.source) {
      this.source = this.control.Options;
      this.mapped = toSelectOptions(this.source);
    }
    return this.mapped;
  }
}

/**
 * "ngselect": a select with search, as cp-atlas-sectionmaster.html (decision D3): the options are mapped to
 * label/value (transformSelect), bindLabel="label" bindValue="value", clearable false, virtual scroll, placeholder "".
 */
@Component({
  selector: 'atlas-ngselect-renderer',
  imports: [ReactiveFormsModule, NgSelectComponent],
  template: `<ng-select [items]="options" bindLabel="label" bindValue="value" [multiple]="!!control.Multiple"
    [clearable]="false" [virtualScroll]="true" placeholder="" [formControl]="formControl" [labelForId]="'ctl_' + control.Name" />`,
})
export class AtlasNgSelectRenderer extends AtlasRendererBase {
  private source: CPKeyValue[] | null | undefined;
  private mapped: SelectOption<string>[] = [];

  get options(): SelectOption<string>[] {
    if (this.control.Options !== this.source) {
      this.source = this.control.Options;
      this.mapped = toSelectOptions(this.source);
    }
    return this.mapped;
  }
}

@Component({
  selector: 'atlas-textarea-renderer',
  imports: [ReactiveFormsModule, TextareaComponent, AtlasControlIdDirective],
  template: `<pbdev-textarea [atlasControlId]="'ctl_' + control.Name" [rows]="control.Row ?? 3" [customErrorMessages]="errorMessages"
    [formControl]="formControl" />`,
})
export class AtlasTextareaRenderer extends AtlasRendererBase {}

@Component({
  selector: 'atlas-date-renderer',
  imports: [ReactiveFormsModule, DatePickerComponent, AtlasControlIdDirective],
  // The form value is a JS Date (the real p-date-picker); the form model sends it as yyyy-MM-dd (review R2).
  template: `<pbdev-date-picker [atlasControlId]="'ctl_' + control.Name" [customErrorMessages]="errorMessages" [formControl]="formControl" />`,
})
export class AtlasDateRenderer extends AtlasRendererBase {}

@Component({
  selector: 'atlas-checkbox-renderer',
  imports: [ReactiveFormsModule, CheckboxComponent, AtlasControlIdDirective],
  template: `<pbdev-checkbox [atlasControlId]="'ctl_' + control.Name" inputLabel="" [formControl]="formControl" />`,
})
export class AtlasCheckboxRenderer extends AtlasRendererBase {}

/** "user": a LAN ID shown as the display name (the option that the server adds). Read-only. */
@Component({
  selector: 'atlas-user-renderer',
  template: `<div class="atlas-readonly" [attr.title]="formControl.value || null">{{ displayName }}</div>`,
})
export class AtlasUserRenderer extends AtlasRendererBase {
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
  template: `@if (rowGroup) {
  <div class="atlas-grid-row" [formGroup]="rowGroup">
    @for (column of columns; track column.field) {
      <label class="atlas-grid-cell" [style.min-width.px]="(column.width ?? 120) * 0.7">
        <span class="atlas-grid-header">{{ column.headerName }}</span>
        <input class="pbdev-control atlas-number" inputmode="numeric" [formControlName]="column.field!" [attr.id]="'ctl_' + column.field" />
        @if (rowGroup.get(column.field!)?.invalid && rowGroup.get(column.field!)?.touched) {
          <span class="atlas-error">{{ cellError(column) }}</span>
        }
      </label>
    }
  </div>
  } @else {
    <div class="atlas-unsupported" role="note">The grid "{{ control.Name }}" needs Grid.BindMode "SingleRowFields".</div>
  }`,
})
export class AtlasGridRowRenderer extends AtlasRendererBase {
  get rowGroup(): FormGroup | null {
    const item = this.group.get(this.control.Name);
    return item instanceof FormGroup ? item : null;
  }

  get columns(): CPGridColumn[] {
    return (this.control.Grid?.Columns ?? []).filter((x) => !!x.field);
  }

  cellError(column: CPGridColumn): string {
    const errors = this.rowGroup?.get(column.field!)?.errors ?? {};
    return errors['server'] ?? this.control.Validator?.Message ?? 'Not valid.';
  }
}

/** "option": one radio button for each option, as the current section master (a plain input type="radio"). */
@Component({
  selector: 'atlas-radio-renderer',
  imports: [ReactiveFormsModule],
  template: `<div class="atlas-radio-group" role="radiogroup">
    @for (option of control.Options ?? []; track $index) {
      <label class="atlas-radio">
        <input type="radio" [attr.id]="$index === 0 ? 'ctl_' + control.Name : null" [name]="'ctl_' + control.Name"
          [value]="option.Value" [formControl]="formControl" />
        {{ option.Label ?? option.Value }}
      </label>
    }
  </div>`,
})
export class AtlasRadioRenderer extends AtlasRendererBase {}

/** A control Type with no renderer (review NG-04): a visible note instead of a missing field. The form disables it. */
@Component({
  selector: 'atlas-unsupported-renderer',
  template: `<div class="atlas-unsupported" role="note">Unsupported control type "{{ control.Type }}".</div>`,
})
export class AtlasUnsupportedRenderer extends AtlasRendererBase implements OnInit {
  ngOnInit(): void {
    if (isDevMode()) {
      console.warn(`Atlas: no renderer for the control type "${this.control.Type}" (${this.control.Name}). Register one with provideAtlasRenderers.`);
    }
  }
}
