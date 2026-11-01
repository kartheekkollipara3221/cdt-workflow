// COPY to the target: WebUi/src/app/platform/form/. Review FABLE section 6.1: one list of effect conditions and one
// list of Computed values that the client (platform.spec.ts) and the server (EffectParityTests, reads this file) both
// check, so the two sides stay the same. Each array is strict JSON: double quotes, no trailing commas, no comments
// inside it.
import { CPEffectCondition } from '../atlas-models';

export interface AtlasEffectCase {
  when: CPEffectCondition | null;
  value: string;
  matches: boolean;
}

export const ATLAS_EFFECT_CASES: AtlasEffectCase[] = [
  { "when": { "HasValue": true }, "value": "", "matches": false },
  { "when": { "HasValue": false }, "value": "", "matches": true },
  { "when": { "Equals": "No" }, "value": "no", "matches": true },
  { "when": { "NotEquals": "No" }, "value": "NO", "matches": false },
  { "when": { "In": ["A", "B"] }, "value": "b", "matches": true },
  { "when": null, "value": "x", "matches": true }
];

/** A Computed expression over the values A, B and C (whole numbers or empty); an empty value is left out. */
export interface AtlasComputedCase {
  expression: string;
  values: Record<string, string>;
  result: number | null;
}

export const ATLAS_COMPUTED_CASES: AtlasComputedCase[] = [
  { "expression": "Sum(A, B, C)", "values": { "A": "1", "B": "", "C": "3" }, "result": 4 },
  { "expression": "Count(A, B, C)", "values": { "A": "1", "B": "", "C": "3" }, "result": 2 },
  { "expression": "Min(A, B, C)", "values": { "A": "5", "B": "", "C": "3" }, "result": 3 },
  { "expression": "Max(A, B, C)", "values": { "A": "", "B": "", "C": "" }, "result": null }
];
