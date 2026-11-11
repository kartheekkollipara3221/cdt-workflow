// COPY to the target: WebUi/src/app/platform/form/.
// Shared test cases for the effect conditions and the Computed values.
// The client tests (platform.spec.ts) and the server tests (EffectParityTests) both check them.
// Thus the client and the server give the same results.
// EffectParityTests reads this file. Each array must be strict JSON: double quotes, no trailing commas, no comments.
import { CPEffectCondition } from '../atlas-models';

/** An effect condition, a control value and the expected result of matches(). */
export interface AtlasEffectCase {
  when: CPEffectCondition | null;
  value: string;
  matches: boolean;
}

/** The effect condition cases. */
export const ATLAS_EFFECT_CASES: AtlasEffectCase[] = [
  { "when": { "HasValue": true }, "value": "", "matches": false },
  { "when": { "HasValue": false }, "value": "", "matches": true },
  { "when": { "Equals": "No" }, "value": "no", "matches": true },
  { "when": { "NotEquals": "No" }, "value": "NO", "matches": false },
  { "when": { "In": ["A", "B"] }, "value": "b", "matches": true },
  { "when": null, "value": "x", "matches": true }
];

/**
 * A Computed expression over the values A, B and C, and the expected result.
 *
 * Each value is a whole number or empty. The expression does not use an empty value.
 */
export interface AtlasComputedCase {
  expression: string;
  values: Record<string, string>;
  result: number | null;
}

/** The Computed value cases. */
export const ATLAS_COMPUTED_CASES: AtlasComputedCase[] = [
  { "expression": "Sum(A, B, C)", "values": { "A": "1", "B": "", "C": "3" }, "result": 4 },
  { "expression": "Count(A, B, C)", "values": { "A": "1", "B": "", "C": "3" }, "result": 2 },
  { "expression": "Min(A, B, C)", "values": { "A": "5", "B": "", "C": "3" }, "result": 3 },
  { "expression": "Max(A, B, C)", "values": { "A": "", "B": "", "C": "" }, "result": null }
];
