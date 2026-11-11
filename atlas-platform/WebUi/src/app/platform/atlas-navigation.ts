// COPY to the target: WebUi/src/app/platform/. Dashboard navigation by FormType and Target.
// The dashboard calls formRoute first. With no FormType, it returns null and the current if chain runs.
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AtlasApiService } from './atlas-api.service';
import { CPApplicationRow, CPButtonTarget } from './atlas-models';

/**
 * Function that returns the route of the dashboard of an app.
 *
 * The form uses it after Close, Save and Delete. The default is the target route "dashboard/:code" (app.routes.ts).
 * A host with another dashboard route provides its own function.
 */
export const ATLAS_DASHBOARD_LINK = new InjectionToken<(code: string) => unknown[]>('ATLAS_DASHBOARD_LINK', {
  providedIn: 'root',
  factory: () => (code: string) => ['/dashboard', code],
});

/**
 * Returns the platform form route for a FormType (FORM, SECTION or STEPPER, any case).
 *
 * @returns The route, or null when the FormType is empty or unknown.
 */
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

/** Returns the FormType of the app with the code (any case), or null. */
export function formTypeOf(apps: CPApplicationRow[], code: string): string | null {
  return apps.find((x) => x.Code?.toUpperCase() === code.toUpperCase())?.FormType ?? null;
}

/**
 * Opens the platform form from the target dashboard (cp-atlas-dashboard.ts).
 *
 * The target dashboard calls it first in onClick and editRow:
 * `if (await this.atlasNavigation.open(code, id)) return;`. Then the current if chain runs.
 */
@Injectable({ providedIn: 'root' })
export class AtlasDashboardNavigation {
  private readonly api = inject(AtlasApiService);
  private readonly router = inject(Router);
  private apps: Promise<CPApplicationRow[]> | null = null;

  /**
   * Opens the form route of the app, by the FormType of its v1/Application row.
   *
   * The service reads the v1/Application rows one time. A target (button) replaces the code and the ID.
   *
   * @returns True when it opened a form. False for an app with no FormType: then it opens nothing.
   */
  async open(code: string | null | undefined, id: number | string, target?: CPButtonTarget | null): Promise<boolean> {
    const appCode = target?.AppCode ?? code;
    if (!appCode) {
      return false;
    }
    const route = formRoute(formTypeOf(await this.rows(), appCode), appCode, Number(target ? (target.Id ?? 0) : id) || 0);
    if (!route) {
      return false;
    }
    await this.router.navigate(route);
    return true;
  }

  private rows(): Promise<CPApplicationRow[]> {
    // A failed read is not kept: the next click reads again.
    this.apps ??= firstValueFrom(this.api.applications()).catch((): CPApplicationRow[] => {
      this.apps = null;
      return [];
    });
    return this.apps;
  }
}
