// COPY to the target: WebUi/Source/App/platform/. Dashboard navigation by FormType and Target (spec 12, question 5).
// The dashboard calls formRoute first; with no FormType it returns null and the current if chain runs.
import { CPApplicationRow } from './atlas-models';

export function formRoute(formType: string | null | undefined, code: string, id: number): unknown[] | null {
  switch ((formType ?? '').toUpperCase()) {
    case 'FORM':
      return ['/form', code, id];
    case 'SECTION':
      return ['/sectionmaster', code, id];
    case 'STEPPER':
      return ['/stepmaster', code, id];
    default:
      return null;
  }
}

export function formTypeOf(apps: CPApplicationRow[], code: string): string | null {
  return apps.find((x) => x.Code?.toUpperCase() === code.toUpperCase())?.FormType ?? null;
}
