// COPY to the target: WebUi/src/app/platform/. Add these routes to app.routes.ts.
// The current routes (dashboard/:code, sectionmaster/:code/:id, stepmaster/...) do not change.
import { Routes } from '@angular/router';

/** Routes of the platform form. */
export const atlasPlatformRoutes: Routes = [
  {
    path: 'form/:code/:id',
    loadComponent: () => import('./form/atlas-form.component').then((m) => m.AtlasFormComponent),
    runGuardsAndResolvers: 'always',
  },
];
