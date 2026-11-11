// COPY to the target: WebUi/src/app/platform/form/renderers/.
// One small component for each control Type. Each renderer gets the control (JSON) and the form group.
// It binds the form control by name. The form component draws the label and the required mark,
// as cp-atlas-sectionmaster.html does.
// pbdev-input, pbdev-select, pbdev-textarea and pbdev-date-picker draw their own error text (pbdev-input-messages).
// They get the platform texts as customErrorMessages. The form draws the error text for the other types.
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
import { CPApplicationControl, CPKeyValue } from '../../atlas-models';
import { errorText } from '../atlas-validators';

/**
 * The base class of a renderer.
 *
 * A renderer of an app extends it. Add it with provideAtlasRenderers({ myType: MyRenderer })
 * or AtlasRendererRegistry.register. The form gives the two inputs.
 */
@Directive()
export abstract class AtlasRendererBase {
  /** The control from the section JSON. */
  @Input({ required: true }) control!: CPApplicationControl;
  /** The form group of the section. */
  @Input({ required: true }) group!: FormGroup;

  /** The form control of this control, found by its name. */
  get formControl(): FormControl {
    return this.group.get(this.control.Name) as FormControl;
  }

  private messagesFor: unknown = null;
  private messages: Record<string, string> = {};

  /**
   * The customErrorMessages for a pbdev control: one platform text for each error key.
   *
   * The texts are the same as on the server. The map is made again only when the errors change.
   */
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

  private optionsFor: CPKeyValue[] | null | undefined;
  private mappedOptions: SelectOption<string>[] = [];

  /**
   * The options as label/value objects (pbdev-select and ng-select take them). The list is mapped again only when the
   * control gets new options.
   */
  get options(): SelectOption<string>[] {
    if (this.control.Options !== this.optionsFor) {
      this.optionsFor = this.control.Options;
      this.mappedOptions = toSelectOptions(this.optionsFor);
    }
    return this.mappedOptions;
  }
}

/** "text": a pbdev-input. */
@Component({
  selector: 'atlas-text-renderer',
  imports: [ReactiveFormsModule, InputComponent, AtlasControlIdDirective],
  template: `<pbdev-input inputType="string" [atlasControlId]="'ctl_' + control.Name" [placeholder]="control.PlaceHolder ?? ''"
    [customErrorMessages]="errorMessages" [formControl]="formControl" />`,
})
export class AtlasTextRenderer extends AtlasRendererBase {}

/**
 * "currency" and "percent": a text input with no mask, as in cp-atlas-sectionmaster.html.
 *
 * The maskF attribute of the target is not an input of pbdev-input, so there is no mask.
 * A value of only the currency sign is cleared.
 */
@Component({
  selector: 'atlas-amount-renderer',
  imports: [ReactiveFormsModule, InputComponent, AtlasControlIdDirective],
  template: `<pbdev-input inputType="text" [atlasControlId]="'ctl_' + control.Name" [placeholder]="control.PlaceHolder ?? ''"
    [customErrorMessages]="errorMessages" [formControl]="formControl" (input)="onInput($event)" />`,
})
export class AtlasAmountRenderer extends AtlasRendererBase {
  /** Clears the value when the input holds only the currency sign "$ ". */
  onInput(event: Event): void {
    if ((event.target as HTMLInputElement | null)?.value === '$ ') {
      this.formControl.setValue('');
      this.formControl.updateValueAndValidity();
    }
  }
}

/** "select": a pbdev-select. */
@Component({
  selector: 'atlas-select-renderer',
  imports: [ReactiveFormsModule, SelectComponent, AtlasControlIdDirective],
  template: `<pbdev-select [atlasControlId]="'ctl_' + control.Name" [options]="options" [customErrorMessages]="errorMessages"
    [formControl]="formControl" />`,
})
export class AtlasSelectRenderer extends AtlasRendererBase {}

/**
 * "ngselect": a select with search, as in cp-atlas-sectionmaster.html.
 *
 * The options are mapped to label/value, as transformSelect does. The settings are bindLabel="label",
 * bindValue="value", clearable false, virtual scroll and placeholder "".
 */
@Component({
  selector: 'atlas-ngselect-renderer',
  imports: [ReactiveFormsModule, NgSelectComponent],
  template: `<ng-select [items]="options" bindLabel="label" bindValue="value" [multiple]="!!control.Multiple"
    [clearable]="false" [virtualScroll]="true" placeholder="" [formControl]="formControl" [labelForId]="'ctl_' + control.Name" />`,
})
export class AtlasNgSelectRenderer extends AtlasRendererBase {}

/** "textarea": a pbdev-textarea with control.Row rows (3 by default). */
@Component({
  selector: 'atlas-textarea-renderer',
  imports: [ReactiveFormsModule, TextareaComponent, AtlasControlIdDirective],
  template: `<pbdev-textarea [atlasControlId]="'ctl_' + control.Name" [rows]="control.Row ?? 3" [customErrorMessages]="errorMessages"
    [formControl]="formControl" />`,
})
export class AtlasTextareaRenderer extends AtlasRendererBase {}

/** "date" and "datetimepicker": a pbdev-date-picker. */
@Component({
  selector: 'atlas-date-renderer',
  imports: [ReactiveFormsModule, DatePickerComponent, AtlasControlIdDirective],
  // The form value is a JS Date, as p-date-picker gives it. The form model sends it as yyyy-MM-dd.
  template: `<pbdev-date-picker [atlasControlId]="'ctl_' + control.Name" [customErrorMessages]="errorMessages" [formControl]="formControl" />`,
})
export class AtlasDateRenderer extends AtlasRendererBase {}

/** "checkbox" and "check": a pbdev-checkbox with no label of its own. */
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
  /** The label of the option for the LAN ID, or the LAN ID. A dash when there is no value. */
  get displayName(): string {
    const value = this.formControl.value as string | null;
    if (!value) {
      return '—';
    }
    return this.control.Options?.find((x) => x.Value === value)?.Label ?? value;
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

/**
 * The renderer of a control Type with no renderer.
 *
 * It shows a visible note instead of a missing field. The form disables the control.
 */
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
