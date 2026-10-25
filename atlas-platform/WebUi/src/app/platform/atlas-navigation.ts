// COPY to the target: WebUi/Source/App/platform/. Dashboard navigation by FormType and Target (spec 12, question 5).
// The dashboard calls formRoute first; with no FormType it returns null and the current if chain runs.
import { InjectionToken } from '@angular/core';
import { CPApplicationRow } from './atlas-models';

/**
 * The route of the dashboard of an app, used by the form (Close, Save, Delete). The default is the target route
 * "dashboard/:code" (app.routes.ts). A host with another dashboard route provides its own function.
 */
export const ATLAS_DASHBOARD_LINK = new InjectionToken<(code: string) => unknown[]>('ATLAS_DASHBOARD_LINK', {
  providedIn: 'root',
  factory: () => (code: string) => ['/dashboard', code],
});

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
