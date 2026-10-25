import { Routes } from '@angular/router';
import { atlasPlatformRoutes } from './platform/atlas-platform.routes';

// Local routes. "dashboard/:code" is the same path as the target route, so the platform form (ATLAS_DASHBOARD_LINK)
// goes back to the dashboard in the same way here and at the target. At the target the current dashboard component
// stays and gets the FormType navigation (INTEGRATION.md).
export const routes: Routes = [
  { path: '', loadComponent: () => import('./stand-in/shell/home.component').then((m) => m.HomeComponent) },
  {
    path: 'dashboard/:code',
    loadComponent: () => import('./platform/dashboard/atlas-dashboard.component').then((m) => m.AtlasDashboardComponent),
    runGuardsAndResolvers: 'always',
  },
  { path: 'apps/:code', redirectTo: 'dashboard/:code' },
  ...atlasPlatformRoutes,
  { path: '**', redirectTo: '' },
];
