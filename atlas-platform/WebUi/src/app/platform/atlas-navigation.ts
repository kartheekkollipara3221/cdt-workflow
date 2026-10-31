// COPY to the target: WebUi/src/app/platform/. Dashboard navigation by FormType and Target (spec 12, question 5).
// The dashboard calls formRoute first; with no FormType it returns null and the current if chain runs.
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AtlasApiService } from './atlas-api.service';
import { CPApplicationRow, CPButtonTarget } from './atlas-models';

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

/**
 * Review 3 RV-11: the hand edit of the target dashboard (cp-atlas-dashboard.ts) in two guarded lines.
 * In onClick and editRow, first: `if (await this.atlasNavigation.open(code, id)) return;` then the current if chain.
 * It reads the v1/Application rows once (FormType) and opens the form route; it returns false (and opens nothing)
 * for an app with no FormType, so the current if chain runs as before.
 */
@Injectable({ providedIn: 'root' })
export class AtlasDashboardNavigation {
  private readonly api = inject(AtlasApiService);
  private readonly router = inject(Router);
  private apps: Promise<CPApplicationRow[]> | null = null;

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
