// COPY to the target: WebUi/src/app/platform/form/. Value-based client rules (spec 01, 5.1).
// Conditions: Equals, NotEquals, In, HasValue. Actions: Show, Hide, Require, Unrequire, Enable, Disable, Set, Clear,
// SetFromOption. No expression language. The server applies all of them again on save and sets the Set, Clear and
// SetFromOption values itself (review B35).
import { isNil } from '../atlas-dates';
import { CPApplicationControl, CPEffectCondition, CPKeyValue } from '../atlas-models';

/** The form state that the effects change. The form component implements it. */
export interface AtlasEffectTarget {
  setVisible(name: string, visible: boolean): void;
  setRequired(name: string, required: boolean): void;
  setEnabled(name: string, enabled: boolean): void;
  setValue(name: string, value: string | null): void;
}

export function hasValue(value: unknown): boolean {
  return !isNil(value) && String(value) !== '';
}

export function matches(when: CPEffectCondition | null | undefined, value: unknown): boolean {
  if (!when) {
    return true;
  }
  const text = hasValue(value) ? String(value) : '';
  const same = (a: string | null | undefined) => (a ?? '').toLowerCase() === text.toLowerCase();
  if (!isNil(when.HasValue) && when.HasValue !== hasValue(value)) {
    return false;
  }
  if (!isNil(when.Equals) && !same(when.Equals)) {
    return false;
  }
  if (!isNil(when.NotEquals) && same(when.NotEquals)) {
    return false;
  }
  if (when.In && !when.In.some((x) => same(x))) {
    return false;
  }
  return true;
}

/**
 * The option data for SetFromOption (spec 01, 5.2): Data of the selected option. With no Data, the current
 * " - " split is used (part 1 = number, part 2 = name), so the current options keep working.
 */
export function optionData(options: CPKeyValue[] | null | undefined, value: unknown): Record<string, string> | null {
  if (!hasValue(value)) {
    return null;
  }
  const option = (options ?? []).find((x) => x.Value === String(value));
  if (option?.Data) {
    return option.Data;
  }
  const parts = String(value).split(' - ');
  return parts.length >= 2 ? { AccountNumber: parts[0].trim(), AccountName: parts.slice(1).join(' - ').trim() } : null;
}

/** Runs the effects of one control for its current value. */
export function applyEffects(control: CPApplicationControl, value: unknown, target: AtlasEffectTarget): void {
  for (const effect of control.Effects ?? []) {
    if (!matches(effect.When, value)) {
      continue;
    }
    effect.Show?.forEach((name) => target.setVisible(name, true));
    effect.Hide?.forEach((name) => target.setVisible(name, false));
    effect.Require?.forEach((name) => target.setRequired(name, true));
    effect.Unrequire?.forEach((name) => target.setRequired(name, false));
    effect.Enable?.forEach((name) => target.setEnabled(name, true));
    effect.Disable?.forEach((name) => target.setEnabled(name, false));
    Object.entries(effect.Set ?? {}).forEach(([name, text]) => target.setValue(name, text));
    effect.Clear?.forEach((name) => target.setValue(name, null));
    if (effect.SetFromOption) {
      const data = optionData(control.Options, value);
      if (data) {
        Object.entries(effect.SetFromOption).forEach(([name, key]) => target.setValue(name, data[key] ?? null));
      }
    }
  }
}
