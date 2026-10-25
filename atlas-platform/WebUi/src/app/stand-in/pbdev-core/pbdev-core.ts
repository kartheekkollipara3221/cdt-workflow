// STAND-IN: do not copy to the target. Local copy of the internal library @pbdev/pbdev-core (spec 12, 11.4).
// Same selectors, inputs, outputs and export names as the uses in cp-atlas-sectionmaster.html. The form controls
// implement ControlValueAccessor, so formControlName and [formControl] work as with the real library.
// Plain HTML and simple CSS. The look and feel and the masks are checked at the target (INTEGRATION.md).
import { Component, EventEmitter, Input, Output, forwardRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

abstract class PbdevValueAccessor implements ControlValueAccessor {
  value: string | null = null;
  disabled = false;
  protected onChange: (value: string | null) => void = () => {};
  protected onTouched: () => void = () => {};

  writeValue(value: unknown): void {
    this.value = value === null || value === undefined ? null : String(value);
  }

  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled = disabled;
  }

  update(value: string | null): void {
    this.value = value;
    this.onChange(value);
  }

  touch(): void {
    this.onTouched();
  }
}

function accessor(type: unknown) {
  return { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => type), multi: true };
}

@Component({
  selector: 'pbdev-input',
  template: `<input class="pbdev-control" [attr.id]="inputId || null" [type]="htmlType" [value]="value ?? ''"
    [disabled]="disabled" [attr.placeholder]="placeholder || null" (input)="update($any($event.target).value)" (blur)="touch()" />`,
  providers: [accessor(PbdevInput)],
})
export class PbdevInput extends PbdevValueAccessor {
  @Input() inputType = 'string';
  @Input() inputId = '';
  @Input() placeholder = '';
  @Input() maskF = '';

  get htmlType(): string {
    return this.inputType === 'number' ? 'number' : 'text';
  }
}

export interface PbdevOption {
  Label?: string | null;
  Value?: string | null;
  label?: string | null;
  value?: string | null;
}

@Component({
  selector: 'pbdev-select',
  template: `<select class="pbdev-control" [disabled]="disabled" (change)="update($any($event.target).value || null)" (blur)="touch()">
    <option value="" [selected]="!value">{{ placeholder || 'Select' }}</option>
    @for (option of options ?? []; track $index) {
      <option [value]="optionValue(option)" [selected]="optionValue(option) === value">{{ optionLabel(option) }}</option>
    }
  </select>`,
  providers: [accessor(PbdevSelect)],
})
export class PbdevSelect extends PbdevValueAccessor {
  @Input() options: PbdevOption[] | null | undefined = [];
  @Input() placeholder = '';

  optionValue(option: PbdevOption): string {
    return option.Value ?? option.value ?? '';
  }

  optionLabel(option: PbdevOption): string {
    return option.Label ?? option.label ?? this.optionValue(option);
  }
}

@Component({
  selector: 'pbdev-textarea',
  template: `<textarea class="pbdev-control" [rows]="rows || 3" [value]="value ?? ''" [disabled]="disabled"
    [attr.placeholder]="placeholder || null" (input)="update($any($event.target).value)" (blur)="touch()"></textarea>`,
  providers: [accessor(PbdevTextarea)],
})
export class PbdevTextarea extends PbdevValueAccessor {
  @Input() rows: number | null | undefined = 3;
  @Input() placeholder = '';
}

@Component({
  selector: 'pbdev-date-picker',
  template: `<input class="pbdev-control" type="date" [value]="value ?? ''" [disabled]="disabled"
    (change)="update($any($event.target).value || null)" (blur)="touch()" />`,
  providers: [accessor(PbdevDatePicker)],
})
export class PbdevDatePicker extends PbdevValueAccessor {}

@Component({
  selector: 'pbdev-checkbox',
  template: `<label class="pbdev-checkbox"><input type="checkbox" [checked]="value === 'true'" [disabled]="disabled"
    (change)="update($any($event.target).checked ? 'true' : 'false')" (blur)="touch()" /> {{ label }}</label>`,
  providers: [accessor(PbdevCheckbox)],
})
export class PbdevCheckbox extends PbdevValueAccessor {
  @Input() label = '';
}

@Component({
  selector: 'pbdev-section',
  template: `<section class="pbdev-section"><ng-content /></section>`,
})
export class PbdevSection {}

@Component({
  selector: 'pbdev-section-label',
  template: `<section class="pbdev-section">
    @if (label) {
      <h2 class="pbdev-section-title">{{ label }}</h2>
    }
    <ng-content />
  </section>`,
})
export class PbdevSectionLabel {
  @Input() label: string | null | undefined = '';
}

@Component({
  selector: 'pbdev-loader',
  template: `<div class="pbdev-loader" role="status" aria-label="Loading"><span></span></div>`,
})
export class PbdevLoader {}

@Component({
  selector: 'pbdev-dialog',
  template: `@if (open) {
    <div class="pbdev-dialog-backdrop" (click)="closed.emit()"></div>
    <div class="pbdev-dialog" role="dialog" [attr.aria-label]="header">
      <header><h3>{{ header }}</h3><button type="button" class="pbdev-dialog-x" (click)="closed.emit()" aria-label="Close">x</button></header>
      <div class="pbdev-dialog-body"><ng-content /></div>
    </div>
  }`,
})
export class PbdevDialog {
  @Input() open = false;
  @Input() header = '';
  @Output() closed = new EventEmitter<void>();
}

export const PBDEV_COMPONENTS = [
  PbdevInput,
  PbdevSelect,
  PbdevTextarea,
  PbdevDatePicker,
  PbdevCheckbox,
  PbdevSection,
  PbdevSectionLabel,
  PbdevLoader,
  PbdevDialog,
] as const;
