// STAND-IN: do not copy to the target. Local copy of the internal library @pbdev/pbdev-core (spec 12, 11.4).
// Export names, selectors, inputs and outputs follow the uses in the target WebUi only (inventory and evidence:
// atlas-platform/PBDEV.md). An input that the target does not use is not here, so the platform cannot depend on it.
// The form controls implement ControlValueAccessor, so formControlName and [formControl] work as with the real library.
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

/** Target: cp-atlas-steppage.ts (label/value objects given to pbdev-select). */
export interface SelectOption {
  label: string;
  value: string;
}

/** Target: cp-atlas-sectionmaster.html (inputType, maskF, inputId), Text-editor.component.ts (full input list). */
@Component({
  selector: 'pbdev-input',
  template: `<input class="pbdev-control" [attr.id]="inputId || null" [attr.name]="inputName || null" [type]="htmlType"
    [value]="value ?? ''" [disabled]="disabled" [attr.placeholder]="placeholder || null"
    (input)="update($any($event.target).value)" (blur)="touch()" />`,
  providers: [accessor(InputComponent)],
})
export class InputComponent extends PbdevValueAccessor {
  @Input() inputType = 'text';
  @Input() inputId = '';
  @Input() inputName = '';
  @Input() inputLabel = '';
  @Input() placeholder = '';
  @Input() helperText = '';
  @Input() maskF = '';
  @Input() mask = '';
  @Input() required = false;

  get htmlType(): string {
    return this.inputType === 'number' ? 'number' : 'text';
  }
}

/** Target: cp-atlas-sectionmaster.html ([options] with SelectOption[]). */
@Component({
  selector: 'pbdev-select',
  template: `<select class="pbdev-control" [disabled]="disabled" (change)="update($any($event.target).value || null)" (blur)="touch()">
    <option value="" [selected]="!value">Select</option>
    @for (option of options ?? []; track $index) {
      <option [value]="option.value" [selected]="option.value === value">{{ option.label }}</option>
    }
  </select>`,
  providers: [accessor(SelectComponent)],
})
export class SelectComponent extends PbdevValueAccessor {
  @Input() options: SelectOption[] | null | undefined = [];
}

/** Target: cp-atlas-sectionmaster.html ([rows]). */
@Component({
  selector: 'pbdev-textarea',
  template: `<textarea class="pbdev-control" [rows]="rows || 3" [value]="value ?? ''" [disabled]="disabled"
    (input)="update($any($event.target).value)" (blur)="touch()"></textarea>`,
  providers: [accessor(TextareaComponent)],
})
export class TextareaComponent extends PbdevValueAccessor {
  @Input() rows: number | null | undefined = 3;
}

/** Target: cp-atlas-sectionmaster.html (formControlName only). */
@Component({
  selector: 'pbdev-date-picker',
  template: `<input class="pbdev-control" type="date" [value]="value ?? ''" [disabled]="disabled"
    (change)="update($any($event.target).value || null)" (blur)="touch()" />`,
  providers: [accessor(DatePickerComponent)],
})
export class DatePickerComponent extends PbdevValueAccessor {}

/** Target: cp-atlas-steppage.html (inputLabel). */
@Component({
  selector: 'pbdev-checkbox',
  template: `<span class="pbdev-checkbox"><input type="checkbox" [checked]="value === 'true'" [disabled]="disabled"
    (change)="update($any($event.target).checked ? 'true' : 'false')" (blur)="touch()" /> {{ inputLabel }}</span>`,
  providers: [accessor(CheckboxComponent)],
})
export class CheckboxComponent extends PbdevValueAccessor {
  @Input() inputLabel = '';
}

/** Target: cp-atlas-steppage.html (<pbdev-section [variant]="'blue'" title="...">). */
@Component({
  selector: 'pbdev-section',
  template: `<section class="pbdev-section" [attr.data-variant]="variant || null">
    @if (title) {
      <h2 class="pbdev-section-title">{{ title }}</h2>
    }
    <ng-content />
  </section>`,
})
export class SectionComponent {
  @Input() variant = '';
  @Input() title: string | null | undefined = '';
}

/** Target: cp-atlas-sectionmaster.html (<pbdev-loader />), app.component.html (variant, widthRem, color). */
@Component({
  selector: 'pbdev-loader',
  template: `<div class="pbdev-loader" role="status" aria-label="Loading"><span></span></div>`,
})
export class LoaderComponent {
  @Input() variant = '';
  @Input() widthRem = '';
  @Input() color = '';
}

/** Target: app.component.html (<pbdev-dialog [(visible)]="..." [header]="...">). */
@Component({
  selector: 'pbdev-dialog',
  template: `@if (visible) {
    <div class="pbdev-dialog-backdrop" (click)="close()"></div>
    <div class="pbdev-dialog" role="dialog" [attr.aria-label]="header">
      <header><h3>{{ header }}</h3><button type="button" class="pbdev-dialog-x" (click)="close()" aria-label="Close">x</button></header>
      <div class="pbdev-dialog-body"><ng-content /></div>
    </div>
  }`,
})
export class DialogComponent {
  @Input() visible = false;
  @Input() header = '';
  @Output() visibleChange = new EventEmitter<boolean>();

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }
}
