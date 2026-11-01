// COPY to the target: WebUi/src/app/platform/form/. Client validators from CPValidator (spec 01, 10.3).
// The server checks the same rules (AtlasFieldValidator); the client checks are for the user only. Both sides use
// the same rules and texts (review SV-04; shared cases in atlas-validator-cases.ts):
//  - a value of only spaces is empty: Required fails, the other checks do not run;
//  - Pattern is anchored as Angular Validators.pattern does (^ and $ added when not there);
//  - Integer, then "a number" (no exponent), then Min and Max;
//  - Validator.Message, when set, is the text of each failure.
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { CPValidator } from '../atlas-models';

const integerText = /^[+-]?\d+$/;
const numberText = /^[+-]?(\d+\.?\d*|\.\d+)$/;

function empty(value: unknown): boolean {
  return value === null || value === undefined || String(value).trim() === '';
}

function text(control: AbstractControl): string {
  return String(control.value);
}

export function atlasRequired(control: AbstractControl): ValidationErrors | null {
  return empty(control.value) ? { required: true } : null;
}

export function atlasMaxLength(max: number): ValidatorFn {
  return (control) => (empty(control.value) || text(control).length <= max ? null : { maxlength: { requiredLength: max } });
}

export function atlasMinLength(min: number): ValidatorFn {
  return (control) => (empty(control.value) || text(control).length >= min ? null : { minlength: { requiredLength: min } });
}

/** The anchoring of Angular Validators.pattern(string), which the server copies. */
export function anchoredPattern(pattern: string): RegExp {
  return new RegExp((pattern.startsWith('^') ? '' : '^') + pattern + (pattern.endsWith('$') ? '' : '$'));
}

export function atlasPattern(pattern: string): ValidatorFn {
  const regex = anchoredPattern(pattern);
  return (control) => (empty(control.value) || regex.test(text(control)) ? null : { pattern: true });
}

export function atlasInteger(control: AbstractControl): ValidationErrors | null {
  return empty(control.value) || integerText.test(text(control).trim()) ? null : { integer: true };
}

export function atlasNumber(control: AbstractControl): ValidationErrors | null {
  return empty(control.value) || numberText.test(text(control).trim()) ? null : { number: true };
}

export function atlasMin(min: number): ValidatorFn {
  return (control) => {
    const value = text(control).trim();
    return empty(control.value) || !numberText.test(value) || Number(value) >= min ? null : { min: { min } };
  };
}

export function atlasMax(max: number): ValidatorFn {
  return (control) => {
    const value = text(control).trim();
    return empty(control.value) || !numberText.test(value) || Number(value) <= max ? null : { max: { max } };
  };
}

export function atlasValidators(validator: CPValidator | null | undefined, required: boolean): ValidatorFn[] {
  const list: ValidatorFn[] = [];
  if (required) {
    list.push(atlasRequired);
  }
  if (!validator) {
    return list;
  }
  if (validator.MaxLength !== null && validator.MaxLength !== undefined) {
    list.push(atlasMaxLength(validator.MaxLength));
  }
  if (validator.MinLength !== null && validator.MinLength !== undefined) {
    list.push(atlasMinLength(validator.MinLength));
  }
  if (validator.Pattern) {
    list.push(atlasPattern(validator.Pattern));
  }
  if (validator.Integer) {
    list.push(atlasInteger);
  }
  const min = validator.Min;
  const max = validator.Max;
  const hasMin = min !== null && min !== undefined;
  const hasMax = max !== null && max !== undefined;
  if (validator.Integer || hasMin || hasMax) {
    list.push(atlasNumber);
  }
  if (hasMin) {
    list.push(atlasMin(min));
  }
  if (hasMax) {
    list.push(atlasMax(max));
  }
  return list;
}

/** The server error code of the first client error (the order of the server checks). */
export function errorCode(errors: ValidationErrors | null): string | null {
  if (!errors) {
    return null;
  }
  const order: [string, string][] = [
    ['required', 'Required'],
    ['maxlength', 'MaxLength'],
    ['minlength', 'MinLength'],
    ['pattern', 'Pattern'],
    ['integer', 'Integer'],
    ['number', 'InvalidNumber'],
    ['min', 'Min'],
    ['max', 'Max'],
  ];
  return order.find(([key]) => errors[key])?.[1] ?? null;
}

/** One text for the first error of a control (the same texts as the server). */
export function errorText(errors: ValidationErrors | null, label: string, validator?: CPValidator | null): string | null {
  if (!errors) {
    return null;
  }
  if (errors['server']) {
    return String(errors['server']);
  }
  if (validator?.Message) {
    return validator.Message;
  }
  switch (errorCode(errors)) {
    case 'Required':
      return `${label} is required.`;
    case 'MaxLength':
      return `${label} can have at most ${errors['maxlength'].requiredLength} characters.`;
    case 'MinLength':
      return `${label} must have at least ${errors['minlength'].requiredLength} characters.`;
    case 'Pattern':
      return `${label} has a format that is not valid.`;
    case 'Integer':
      return `${label} must be a whole number.`;
    case 'InvalidNumber':
      return `${label} must be a number.`;
    case 'Min':
      return `${label} must be ${errors['min'].min} or more.`;
    case 'Max':
      return `${label} must be ${errors['max'].max} or less.`;
    default:
      return `${label} is not valid.`;
  }
}
