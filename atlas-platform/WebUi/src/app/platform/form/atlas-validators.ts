// COPY to the target: WebUi/src/app/platform/form/.
// Client validators from CPValidator. The server checks the same rules (AtlasFieldValidator).
// The client checks only help the user. Both sides use the same rules and texts.
// The shared test cases are in atlas-validator-cases.ts. The rules:
//  - A value of only spaces is empty. Required fails, and the other checks do not run.
//  - MustBeTrue fails for each value that is not true, also for an empty value.
//  - Pattern is anchored as Angular Validators.pattern does. It adds ^ and $ when they are not there.
//  - The number checks run in this order: Integer, then "a number" (no exponent), then Min, Max and Step.
//  - When Validator.Message is set, it is the text of each failure.
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { isNil } from '../atlas-dates';
import { CPValidator } from '../atlas-models';

const integerText = /^[+-]?\d+$/;
const numberText = /^[+-]?(\d+\.?\d*|\.\d+)$/;

function empty(value: unknown): boolean {
  return isNil(value) || String(value).trim() === '';
}

function text(control: AbstractControl): string {
  return String(control.value);
}

/** Fails with `required` when the value is empty or has only spaces. */
export function atlasRequired(control: AbstractControl): ValidationErrors | null {
  return empty(control.value) ? { required: true } : null;
}

/** Fails with `mustBeTrue` when the value is not true: a boolean true, or the text true, yes or 1 (any case). */
export function atlasMustBeTrue(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  const isTrue = value === true || ['true', 'yes', '1'].includes(String(value ?? '').trim().toLowerCase());
  return isTrue ? null : { mustBeTrue: true };
}

/** Returns a validator that fails with `maxlength` when a value has more than max characters. */
export function atlasMaxLength(max: number): ValidatorFn {
  return (control) => (empty(control.value) || text(control).length <= max ? null : { maxlength: { requiredLength: max } });
}

/** Returns a validator that fails with `minlength` when a value has fewer than min characters. */
export function atlasMinLength(min: number): ValidatorFn {
  return (control) => (empty(control.value) || text(control).length >= min ? null : { minlength: { requiredLength: min } });
}

/** Returns the pattern anchored as Angular Validators.pattern(string) does. The server uses the same anchoring. */
export function anchoredPattern(pattern: string): RegExp {
  return new RegExp((pattern.startsWith('^') ? '' : '^') + pattern + (pattern.endsWith('$') ? '' : '$'));
}

/** Returns a validator that fails with `pattern` when the full value does not match the pattern. */
export function atlasPattern(pattern: string): ValidatorFn {
  const regex = anchoredPattern(pattern);
  return (control) => (empty(control.value) || regex.test(text(control)) ? null : { pattern: true });
}

/** Fails with `integer` when the value is not a whole number. */
export function atlasInteger(control: AbstractControl): ValidationErrors | null {
  return empty(control.value) || integerText.test(text(control).trim()) ? null : { integer: true };
}

/** Fails with `number` when the value is not a decimal number. An exponent is not valid. */
export function atlasNumber(control: AbstractControl): ValidationErrors | null {
  return empty(control.value) || numberText.test(text(control).trim()) ? null : { number: true };
}

/** Returns a validator that fails with `min` when a number is less than min. */
export function atlasMin(min: number): ValidatorFn {
  return (control) => {
    const value = text(control).trim();
    return empty(control.value) || !numberText.test(value) || Number(value) >= min ? null : { min: { min } };
  };
}

/** Returns a validator that fails with `max` when a number is more than max. */
export function atlasMax(max: number): ValidatorFn {
  return (control) => {
    const value = text(control).trim();
    return empty(control.value) || !numberText.test(value) || Number(value) <= max ? null : { max: { max } };
  };
}

/** Returns a validator that fails with `step` when a number is not a multiple of step. */
export function atlasStep(step: number): ValidatorFn {
  return (control) => {
    const value = text(control).trim();
    if (empty(control.value) || !numberText.test(value)) {
      return null;
    }
    const ratio = Number(value) / step;
    // A decimal step (0.1) gives a ratio that is not exact in binary.
    return Math.abs(ratio - Math.round(ratio)) < 1e-9 ? null : { step: { step } };
  };
}

/**
 * Returns the client validators of a control.
 *
 * @param required True when the field is required now. Effects can change it, so it can differ from
 *     Validator.Required.
 */
export function atlasValidators(validator: CPValidator | null | undefined, required: boolean): ValidatorFn[] {
  const list: ValidatorFn[] = [];
  if (required) {
    list.push(atlasRequired);
  }
  if (!validator) {
    return list;
  }
  if (validator.MustBeTrue) {
    list.push(atlasMustBeTrue);
  }
  if (!isNil(validator.MaxLength)) {
    list.push(atlasMaxLength(validator.MaxLength));
  }
  if (!isNil(validator.MinLength)) {
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
  const hasMin = !isNil(min);
  const hasMax = !isNil(max);
  const step = validator.Step;
  const hasStep = !isNil(step) && step > 0;
  if (validator.Integer || hasMin || hasMax || hasStep) {
    list.push(atlasNumber);
  }
  if (hasMin) {
    list.push(atlasMin(min));
  }
  if (hasMax) {
    list.push(atlasMax(max));
  }
  if (hasStep) {
    list.push(atlasStep(step));
  }
  return list;
}

/** Returns the server error code of the first client error, in the order of the server checks. */
export function errorCode(errors: ValidationErrors | null): string | null {
  if (!errors) {
    return null;
  }
  const order: [string, string][] = [
    ['required', 'Required'],
    ['mustBeTrue', 'MustBeTrue'],
    ['maxlength', 'MaxLength'],
    ['minlength', 'MinLength'],
    ['pattern', 'Pattern'],
    ['integer', 'Integer'],
    ['number', 'InvalidNumber'],
    ['min', 'Min'],
    ['max', 'Max'],
    ['step', 'Step'],
  ];
  return order.find(([key]) => errors[key])?.[1] ?? null;
}

/**
 * Returns one text for the first error of a control. The texts are the same as on the server.
 *
 * A server error comes first, then Validator.Message, then the default text. Returns null when there is no error.
 */
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
    case 'MustBeTrue':
      return `Select ${label} before you submit.`;
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
    case 'Step':
      return `${label} must be a multiple of ${errors['step'].step}.`;
    default:
      return `${label} is not valid.`;
  }
}
