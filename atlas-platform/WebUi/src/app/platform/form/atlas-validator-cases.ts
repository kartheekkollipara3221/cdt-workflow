// COPY to the target: WebUi/src/app/platform/form/.
// Shared validator test cases. The client tests (platform.spec.ts) and the server tests (ValidatorParityTests)
// both check them. Thus the client and the server rules give the same results.
// ValidatorParityTests reads this file. The array must be strict JSON: double quotes, no trailing commas, no comments.
// The label of the field is "Field".
import { CPValidator } from '../atlas-models';

/** A validator, a value and the expected error code and message. */
export interface AtlasValidatorCase {
  validator: CPValidator;
  value: string;
  /** The server error code, or null for a valid value. */
  code: string | null;
  /** The error text, or null for a valid value. */
  message: string | null;
}

/** The validator cases. */
export const ATLAS_VALIDATOR_CASES: AtlasValidatorCase[] = [
  { "validator": { "Required": true }, "value": "   ", "code": "Required", "message": "Field is required." },
  { "validator": { "Required": true, "Message": "Enter it." }, "value": "", "code": "Required", "message": "Enter it." },
  { "validator": { "Required": true }, "value": "x", "code": null, "message": null },
  { "validator": { "MaxLength": 3 }, "value": "abcd", "code": "MaxLength", "message": "Field can have at most 3 characters." },
  { "validator": { "MaxLength": 3, "Message": "Too long." }, "value": "abcd", "code": "MaxLength", "message": "Too long." },
  { "validator": { "MaxLength": 3 }, "value": "abc", "code": null, "message": null },
  { "validator": { "MinLength": 2 }, "value": "a", "code": "MinLength", "message": "Field must have at least 2 characters." },
  { "validator": { "MinLength": 2 }, "value": "  ", "code": null, "message": null },
  { "validator": { "Pattern": "\\d{4}" }, "value": "abc1234xyz", "code": "Pattern", "message": "Field has a format that is not valid." },
  { "validator": { "Pattern": "\\d{4}" }, "value": "1234", "code": null, "message": null },
  { "validator": { "Pattern": "^[A-Z]{3}" }, "value": "ABCD", "code": "Pattern", "message": "Field has a format that is not valid." },
  { "validator": { "Integer": true }, "value": "1.5", "code": "Integer", "message": "Field must be a whole number." },
  { "validator": { "Integer": true }, "value": "", "code": null, "message": null },
  { "validator": { "Min": 0, "Max": 10 }, "value": "1e3", "code": "InvalidNumber", "message": "Field must be a number." },
  { "validator": { "Min": 0 }, "value": "-1", "code": "Min", "message": "Field must be 0 or more." },
  { "validator": { "Min": 1 }, "value": ".5", "code": "Min", "message": "Field must be 1 or more." },
  { "validator": { "Max": 1440 }, "value": "1441", "code": "Max", "message": "Field must be 1440 or less." },
  { "validator": { "Max": 1440 }, "value": "1440", "code": null, "message": null },
  { "validator": { "Max": 10, "Message": "Too big." }, "value": "11", "code": "Max", "message": "Too big." },
  { "validator": { "Step": 5 }, "value": "7", "code": "Step", "message": "Field must be a multiple of 5." },
  { "validator": { "Step": 5 }, "value": "0", "code": null, "message": null },
  { "validator": { "Step": 5 }, "value": "1440", "code": null, "message": null },
  { "validator": { "Step": 5 }, "value": "", "code": null, "message": null },
  { "validator": { "Step": 5 }, "value": "abc", "code": "InvalidNumber", "message": "Field must be a number." },
  { "validator": { "Step": 0.1 }, "value": "0.3", "code": null, "message": null },
  { "validator": { "Step": 0.1 }, "value": "0.35", "code": "Step", "message": "Field must be a multiple of 0.1." },
  { "validator": { "Max": 1440, "Step": 5 }, "value": "1441", "code": "Max", "message": "Field must be 1440 or less." },
  { "validator": { "Integer": true, "Step": 5, "Message": "Steps of 5." }, "value": "12", "code": "Step", "message": "Steps of 5." }
];
