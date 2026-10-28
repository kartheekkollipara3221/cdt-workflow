// STAND-IN: do not copy to the target. Local copy of the internal library @pbdev/pbdev-core 6.0.x (spec 12, 11.4).
// Since 2026-10-27 it follows the REAL library source (target-implementation-read-only/PB-DEV-COMPONENTS, review R1):
// the same selectors, @Input/@Output names and types, and the same value behaviour:
//  - the form controls do not convert values: the host FormControl is bound to the inner element
//    (CustomControlValueAccessorDirective), so the inner element sets the value type;
//  - pbdev-checkbox gives a boolean, pbdev-date-picker gives a JS Date (PrimeNG p-date-picker at the target),
//    pbdev-select gives the raw option value;
//  - pbdev-input, pbdev-textarea, pbdev-select and pbdev-date-picker draw their own error text above the element
//    (pbdev-input-messages, pbdev-control-errors; default texts for "required" and "email", else "Error");
//  - inputId is a field (a generated id), not an @Input;
//  - pbdev-dialog does not change its own "visible"; it emits visibleChange(false).
// Plain HTML instead of PrimeNG and Maskito. The look is checked at the target (INTEGRATION.md).
import { KeyValuePipe, NgClass } from '@angular/common';
import {
  Component,
  Directive,
  EventEmitter,
  HostBinding,
  HostListener,
  Injector,
  Input,
  OnInit,
  Output,
  forwardRef,
  inject,
} from '@angular/core';
import {
  AbstractControl,
  ControlValueAccessor,
  FormControl,
  FormControlDirective,
  FormControlName,
  FormGroupDirective,
  NG_VALUE_ACCESSOR,
  NgControl,
  NgModel,
  ReactiveFormsModule,
  ValidationErrors,
} from '@angular/forms';
import type { ILoadingOverlayAngularComp } from 'ag-grid-angular';
import type { ILoadingOverlayParams } from 'ag-grid-community';

let nextId = 0;
function newId(): string {
  nextId += 1;
  return `pbdev-${nextId}`;
}

/** As the real CustomControlValueAccessorDirective: it binds the host FormControl to the inner element. */
@Directive()
export abstract class CustomControlValueAccessorDirective<T> implements ControlValueAccessor, OnInit {
  protected injector = inject(Injector);
  control!: FormControl<T>;
  _isDisabled = false;
  _onTouched!: () => T;

  get isRequired(): boolean {
    return !!this.control?.validator?.({} as AbstractControl)?.['required'];
  }

  ngOnInit(): void {
    this.setFormControl();
  }

  setFormControl(): void {
    const ngControl = this.injector.get(NgControl, null, { optional: true });
    if (ngControl instanceof NgModel) {
      this.control = ngControl.control;
    } else if (ngControl instanceof FormControlDirective) {
      this.control = ngControl.form;
    } else if (ngControl instanceof FormControlName) {
      this.control = this.injector.get(FormGroupDirective).getControl(ngControl);
    } else {
      this.control = new FormControl();
    }
    this.control.markAsPristine({ onlySelf: true });
  }

  writeValue(_value: T): void {
    return;
  }

  registerOnChange(_fn: () => void): void {
    return;
  }

  registerOnTouched(fn: () => T): void {
    this._onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this._isDisabled = isDisabled;
  }
}

function accessor(type: unknown) {
  return { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => type), multi: true };
}

/** Real: src/components/select/select-option.interface (value is generic). */
export interface SelectOption<T = unknown> {
  label: string;
  value: T;
}

/** Real: the default masks of pbdev-input (Maskito). The stand-in accepts the key and does not format. */
export type DefaultMaskKey = 'amount' | 'percent' | 'positiveInt' | 'phone';

@Component({
  selector: 'pbdev-control-errors',
  imports: [KeyValuePipe],
  template: `<ul class="pbdev-control-errors">
    @for (error of errors | keyvalue; track error.key) {
      <li>{{ messages[error.key] || 'Error' }}</li>
    }
  </ul>`,
})
export class ControlErrorsComponent {
  @Input() errors: ValidationErrors | undefined = undefined;
  defaultMessages: Record<string, string> = {
    required: 'This field is required',
    email: 'Incorrect Email Address Format',
  };
  @Input() set customMessages(value: Record<string, string> | undefined) {
    this.messages = { ...this.defaultMessages, ...value };
  }
  messages: Record<string, string> = this.defaultMessages;
}

@Component({
  selector: 'pbdev-input-messages',
  imports: [ControlErrorsComponent],
  template: `@if (helperText && !showError) {
      <p class="helper-text">{{ helperText }}</p>
    }
    @if (showError) {
      <pbdev-control-errors [errors]="control.errors!" [customMessages]="customErrorMessages" />
    }`,
})
export class InputMessagesComponent {
  @Input({ required: true }) control!: FormControl;
  @Input() helperText?: string;
  @Input() customErrorMessages?: Record<string, string>;
  @Input() customShowError?: boolean;

  get showError(): boolean {
    if (this.customShowError !== undefined) {
      return this.control.invalid && this.customShowError;
    }
    return this.control.invalid && this.control.touched;
  }
}

@Component({
  selector: 'pbdev-input-label',
  template: `<label class="input-label" [attr.for]="inputId">{{ inputLabel }}@if (isRequired) {<span>*</span>}</label>`,
})
export class InputLabelComponent {
  @Input() inputId = '';
  @Input() inputLabel?: string = '';
  @Input() isRequired = false;
}

/** Real: src/components/input. No maskF, no required, no inputId input. */
@Component({
  selector: 'pbdev-input',
  imports: [ReactiveFormsModule, InputMessagesComponent, InputLabelComponent],
  template: `@if (variant !== inlineVariant) {
      <pbdev-input-messages [control]="control" [helperText]="helperText" [customErrorMessages]="customErrorMessages" />
    }
    <input [id]="inputId" [type]="inputType" autocomplete="off" [placeholder]="placeholder" [name]="inputName" [formControl]="control" />
    @if (inputLabel && variant !== inlineVariant) {
      <pbdev-input-label [inputId]="inputId" [isRequired]="isRequired && isRequiredIndicatorVisible" [inputLabel]="inputLabel" />
    }`,
  providers: [accessor(InputComponent)],
})
export class InputComponent<T> extends CustomControlValueAccessorDirective<T> {
  readonly inlineVariant: string = 'inline';
  inputId: string = newId();

  @Input() isRequiredIndicatorVisible = true;
  @Input({ required: true }) inputType!: string;
  @Input() customErrorMessages?: Record<string, string>;
  @Input() inputLabel?: string;
  @Input() helperText?: string;
  @Input() iconName?: string = '';
  @Input() inputName: string = '';
  @Input() placeholder: string = '';
  @Input() variant: 'light' | 'dark' | 'inline' = 'light';
  /** Real: a Maskito mask. The stand-in does not format (the platform uses no mask, decision D1). */
  @Input() mask: DefaultMaskKey | object | null = null;

  @HostBinding('class') get inputVariant() {
    return this.variant;
  }

  @HostBinding('class.error') get hasError() {
    return !!this.control.errors && this.control.touched;
  }

  @HostBinding('class.disabled') get isDisabled() {
    return this._isDisabled;
  }
}

/** Real: src/components/select (source not supplied; API from the stories and pbdev-select-alt). */
@Component({
  selector: 'pbdev-select',
  imports: [ReactiveFormsModule, InputMessagesComponent, InputLabelComponent],
  template: `@if (variant !== 'inline') {
      <pbdev-input-messages [control]="control" [helperText]="helperText" [customErrorMessages]="customErrorMessages" />
    }
    <select [id]="inputId" [formControl]="control" (change)="selected.emit(control.value)">
      @for (option of options ?? []; track $index) {
        <option [ngValue]="option.value">{{ option.label }}</option>
      }
    </select>
    @if (inputLabel && variant !== 'inline') {
      <pbdev-input-label [inputId]="inputId" [inputLabel]="inputLabel" [isRequired]="isRequired && isRequiredIndicatorVisible" />
    }`,
  providers: [accessor(SelectComponent)],
})
export class SelectComponent<T = unknown> extends CustomControlValueAccessorDirective<T> {
  inputId: string = newId();
  @Input() inputLabel?: string = '';
  @Input() helperText?: string;
  @Input() isRequiredIndicatorVisible = true;
  @Input() customErrorMessages?: Record<string, string>;
  @Input() variant: 'light' | 'dark' | 'inline' = 'light';
  @Input() options: SelectOption<T>[] | null | undefined = [];
  @Output() selected = new EventEmitter<T>();

  @HostBinding('class.error') get hasError() {
    return this.control.invalid && this.control.touched;
  }

  @HostBinding('class.disabled') get isDisabled() {
    return this._isDisabled;
  }
}

/** Real: src/components/textarea. */
@Component({
  selector: 'pbdev-textarea',
  imports: [ReactiveFormsModule, InputMessagesComponent, InputLabelComponent],
  template: `<pbdev-input-messages [control]="control" [helperText]="helperText" [customErrorMessages]="customErrorMessages" />
    <textarea autocomplete="off" [id]="inputId" [rows]="rows" [placeholder]="placeholder" [name]="inputName" [formControl]="control"></textarea>
    @if (inputLabel) {
      <pbdev-input-label [inputId]="inputId" [inputLabel]="inputLabel" [isRequired]="isRequired && isRequiredIndicatorVisible" />
    }`,
  providers: [accessor(TextareaComponent)],
})
export class TextareaComponent extends CustomControlValueAccessorDirective<string> {
  @Input() inputName: string = '';
  @Input() inputLabel: string = '';
  @Input() placeholder: string = '';
  @Input() helperText?: string;
  @Input() rows: number = 3;
  @Input() customErrorMessages?: Record<string, string>;
  @Input() isRequiredIndicatorVisible = true;
  @Input() @HostBinding('class') variant: 'light' | 'dark' = 'light';
  inputId: string = newId();

  @HostBinding('class.error') get hasError() {
    return !!this.control.errors && this.control.touched;
  }

  @HostBinding('class.disabled') get isDisabled() {
    return this._isDisabled;
  }
}

/**
 * Real: src/components/date-picker (PrimeNG p-date-picker). The value is a JS Date (local midnight), or null.
 * A value that is not a Date is not shown (PrimeNG does not show a text value either), so a wrong binding shows here.
 */
@Component({
  selector: 'pbdev-date-picker',
  imports: [InputMessagesComponent, InputLabelComponent],
  template: `<pbdev-input-messages [control]="control" [helperText]="helperText" [customErrorMessages]="customErrorMessages" />
    <input type="date" [id]="inputId" [attr.name]="inputName || null" [value]="text" [disabled]="_isDisabled"
      [attr.placeholder]="placeholder || null" [readOnly]="readonly" (change)="change($any($event.target).value)" (blur)="blur()" />
    @if (inputLabel && !inline) {
      <pbdev-input-label [inputId]="inputId" [inputLabel]="inputLabel" [isRequired]="isRequired && isRequiredIndicatorVisible" />
    }`,
  providers: [accessor(DatePickerComponent)],
})
export class DatePickerComponent<T> extends CustomControlValueAccessorDirective<T> {
  @Input() isRequiredIndicatorVisible = true;
  @Input() hideOnDateSelect: boolean = true;
  @Input() inline: boolean = false;
  @Input() readonly: boolean = false;
  @Input() variant: 'light' | 'dark' = 'light';
  @Input() inputLabel?: string;
  @Input() maxDate: Date | undefined | null;
  @Input() minDate: Date | undefined | null;
  @Input() disabledDates?: Date[];
  @Input() helperText?: string;
  @Input() customErrorMessages: Record<string, string> | undefined = undefined;
  @Input() placeholder?: string;
  @Input() inputName?: string;
  @Input() selectionMode: 'multiple' | 'range' | 'single' = 'single';
  // eslint-disable-next-line @angular-eslint/no-output-on-prefix
  @Output() onSelect: EventEmitter<Date> = new EventEmitter<Date>();

  inputId: string = newId();

  @HostBinding('class') get inputVariant() {
    return this.variant;
  }

  @HostBinding('class.error') get hasError() {
    return this.control.invalid && this.control.touched;
  }

  @HostBinding('class.disabled') get isDisabled() {
    return this._isDisabled;
  }

  get text(): string {
    const value = this.control?.value as unknown;
    if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
      return '';
    }
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
  }

  change(text: string): void {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text ?? '');
    const date = match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
    this.control.setValue(date as T);
    this.control.markAsDirty();
    if (date) {
      this.onSelect.emit(date);
    }
  }

  blur(): void {
    this.control.markAsTouched();
  }
}

/** Real: src/components/checkbox. A native checkbox bound to the host control: the value is a boolean. */
@Component({
  selector: 'pbdev-checkbox',
  imports: [ReactiveFormsModule, NgClass],
  template: `<label [for]="inputId" [ngClass]="variant" [class.indeterminate]="indeterminate" [class.disabled]="_isDisabled" [class.error]="hasError">
    <input type="checkbox" [id]="inputId" [formControl]="control" [name]="inputName" (change)="indeterminateChanged()" />
    @if (inputLabel) {
      <span>{{ inputLabel }}</span>
    }
  </label>`,
  providers: [accessor(CheckboxComponent)],
})
export class CheckboxComponent<T> extends CustomControlValueAccessorDirective<T> {
  inputId: string = newId();
  @Input() inputName: string = '';
  @Input() inputLabel?: string;
  @Input() variant?: 'multiselect' | 'default' = 'default';
  @Input() indeterminate: boolean = false;
  @Output() indeterminateChange = new EventEmitter();

  get hasError(): boolean {
    return this.control.invalid && this.control.touched;
  }

  indeterminateChanged(): void {
    if (this.indeterminate) {
      this.indeterminateChange.emit(this.control.value);
    }
  }
}

/** Real: src/components/section. The title is a <p class="section-header--title">. */
@Component({
  selector: 'pbdev-section',
  template: `<div class="section" [class.section-blue]="variant === 'blue' && !collapsible" [class.section-accent]="variant === 'accent' && !collapsible"
    [style.width]="width" [style.height]="height">
    @if (!!title || collapsible) {
      <div class="section-header" [class.clickable]="collapsible" (click)="toggleContent()" (keyup.enter)="toggleContent()" tabindex="0">
        <p class="section-header--title">{{ title ?? '' }}</p>
      </div>
    }
    @if (expanded) {
      <div class="section-content" [class.no-header]="!title && !collapsible">
        <div class="content-wrapper"><ng-content /></div>
      </div>
    }
  </div>`,
})
export class SectionComponent {
  @Input() height?: string;
  @Input() width?: string;
  @Input() collapsible = false;
  @Input() title?: string | null;
  @Input() variant: 'white' | 'blue' | 'accent' = 'white';
  @Input() expanded = true;
  @Output() expandedChange = new EventEmitter<boolean>();

  toggleContent(): void {
    if (this.collapsible) {
      this.expanded = !this.expanded;
      this.expandedChange.emit(this.expanded);
    }
  }
}

/** Real: src/components/loader. */
@Component({
  selector: 'pbdev-loader',
  template: `@switch (variant) {
    @case ('loader-1') {
      <div [class]="'loader ' + _class" [style.width.rem]="widthRem" role="status" aria-label="Loading"></div>
    }
    @case ('loader-2') {
      <div class="loader-2" role="status" aria-label="Loading"></div>
    }
  }`,
})
export class LoaderComponent {
  @Input() variant: 'loader-1' | 'loader-2' = 'loader-2';
  @Input() widthRem?: number = 1.875;
  protected _class: 'loader--white' | 'loader--black' = 'loader--black';

  @Input() set color(value: 'white' | 'black') {
    this._class = value === 'white' ? 'loader--white' : 'loader--black';
  }
}

export interface DialogStyle {
  height?: string;
  width?: string;
}

/** Real: src/components/dialog (PrimeNG p-dialog, modal). It emits visibleChange; the parent sets visible. */
@Component({
  selector: 'pbdev-dialog',
  template: `@if (visible) {
    <div class="pbdev-dialog-mask" (click)="dismissibleMask && onVisibleChange(false)"></div>
    <div class="pbdev-dialog" role="dialog" aria-modal="true" [attr.aria-label]="header || null" [style.width]="_style.width" [style.height]="_style.height">
      <div class="pbdev-dialog-header">
        <span class="pbdev-dialog-title">{{ header }}</span>
        <button type="button" class="pbdev-dialog-close" (click)="onVisibleChange(false)" aria-label="Close">×</button>
      </div>
      <div class="pbdev-dialog-content"><ng-content /></div>
    </div>
  }`,
})
export class DialogComponent {
  private static readonly DEFAULT_WIDTH = '35rem';
  private _defaultStyle: DialogStyle = { width: DialogComponent.DEFAULT_WIDTH };
  protected _style: DialogStyle = this._defaultStyle;

  @Input() visible: boolean = false;
  @Input() dismissibleMask: boolean = true;
  @Input() closeOnEscape: boolean = true;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Input() header?: string;
  @Input() set style(value: DialogStyle) {
    this._style = { ...this._defaultStyle, ...value };
  }

  @HostListener('document:keydown.escape')
  escape(): void {
    if (this.visible && this.closeOnEscape) {
      this.onVisibleChange(false);
    }
  }

  protected onVisibleChange(value: boolean): void {
    this.visibleChange.emit(value);
  }
}

/** Real: src/components/ag-loading-overlay (loadingOverlayComponent of an ag-grid). */
@Component({
  selector: 'pbdev-ag-loading-overlay',
  imports: [LoaderComponent],
  template: `<div class="ag-overlay-loading-center"><pbdev-loader /></div>`,
})
export class AgLoadingOverlayComponent implements ILoadingOverlayAngularComp {
  public params!: ILoadingOverlayParams;

  agInit(params: ILoadingOverlayParams): void {
    this.params = params;
  }

  refresh?(): void {
    return;
  }
}
