// COPY to the target: WebUi/Source/App/platform/form/. Review SV-04: one list of validator cases that the client
// (platform.spec.ts) and the server (ValidatorParityTests, reads this file) both check, so the two rule sets stay the
// same. The array is strict JSON: double quotes, no trailing commas, no comments inside it. The label is "Field".
import { CPValidator } from '../atlas-models';

export interface AtlasValidatorCase {
  validator: CPValidator;
  value: string;
  /** The server error code, or null for a valid value. */
  code: string | null;
  message: string | null;
}

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
  { "validator": { "Max": 10, "Message": "Too big." }, "value": "11", "code": "Max", "message": "Too big." }
];
