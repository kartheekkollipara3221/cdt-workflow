// COPY to the target: WebUi/Source/App/platform/form/. Client validators from CPValidator (spec 01, 10.3).
// The server checks the same rules (ConfigSectionService); the client checks are for the user only.
import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { CPValidator } from '../atlas-models';

const integerText = /^[+-]?\d+$/;

function empty(value: unknown): boolean {
  return value === null || value === undefined || String(value).trim() === '';
}

export function atlasInteger(control: AbstractControl): ValidationErrors | null {
  return empty(control.value) || integerText.test(String(control.value).trim()) ? null : { integer: true };
}

export function atlasMin(min: number): ValidatorFn {
  return (control) => {
    if (empty(control.value)) {
      return null;
    }
    const number = Number(String(control.value).trim());
    return Number.isNaN(number) || number < min ? { min: { min } } : null;
  };
}

export function atlasMax(max: number): ValidatorFn {
  return (control) => {
    if (empty(control.value)) {
      return null;
    }
    const number = Number(String(control.value).trim());
    return Number.isNaN(number) || number > max ? { max: { max } } : null;
  };
}

export function atlasValidators(validator: CPValidator | null | undefined, required: boolean): ValidatorFn[] {
  const list: ValidatorFn[] = [];
  if (required) {
    list.push(Validators.required);
  }
  if (!validator) {
    return list;
  }
  if (validator.MaxLength !== null && validator.MaxLength !== undefined) {
    list.push(Validators.maxLength(validator.MaxLength));
  }
  if (validator.MinLength !== null && validator.MinLength !== undefined) {
    list.push(Validators.minLength(validator.MinLength));
  }
  if (validator.Pattern) {
    list.push(Validators.pattern(validator.Pattern));
  }
  if (validator.Integer) {
    list.push(atlasInteger);
  }
  if (validator.Min !== null && validator.Min !== undefined) {
    list.push(atlasMin(validator.Min));
  }
  if (validator.Max !== null && validator.Max !== undefined) {
    list.push(atlasMax(validator.Max));
  }
  return list;
}

/** One text for the first error of a control. */
export function errorText(errors: ValidationErrors | null, label: string, validator?: CPValidator | null): string | null {
  if (!errors) {
    return null;
  }
  if (errors['server']) {
    return String(errors['server']);
  }
  if (errors['required']) {
    return `${label} is required.`;
  }
  if (validator?.Message) {
    return validator.Message;
  }
  if (errors['maxlength']) {
    return `${label} can have at most ${errors['maxlength'].requiredLength} characters.`;
  }
  if (errors['minlength']) {
    return `${label} must have at least ${errors['minlength'].requiredLength} characters.`;
  }
  if (errors['integer']) {
    return `${label} must be a whole number.`;
  }
  if (errors['min']) {
    return `${label} must be ${errors['min'].min} or more.`;
  }
  if (errors['max']) {
    return `${label} must be ${errors['max'].max} or less.`;
  }
  return `${label} is not valid.`;
}
