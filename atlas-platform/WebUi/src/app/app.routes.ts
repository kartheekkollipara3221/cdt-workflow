import { Routes } from '@angular/router';
import { atlasPlatformRoutes } from './platform/atlas-platform.routes';

// Local routes. "apps/:code" is the local dashboard of a platform app; at the target the current
// "dashboard/:code" route stays and gets the FormType navigation (INTEGRATION.md).
export const routes: Routes = [
  { path: '', loadComponent: () => import('./stand-in/shell/home.component').then((m) => m.HomeComponent) },
  {
    path: 'apps/:code',
    loadComponent: () => import('./platform/dashboard/atlas-dashboard.component').then((m) => m.AtlasDashboardComponent),
    runGuardsAndResolvers: 'always',
  },
  ...atlasPlatformRoutes,
  { path: '**', redirectTo: '' },
];
