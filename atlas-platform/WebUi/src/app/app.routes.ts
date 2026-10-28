import { Routes } from '@angular/router';
import { atlasPlatformRoutes } from './platform/atlas-platform.routes';

// STAND-IN: the target main.ts registers the ag-grid modules (AllEnterpriseModule) for all grids. Here the community
// modules are registered before a dashboard or form route opens, so they stay in a lazy chunk.
const registerGridModules = () =>
  import('ag-grid-community').then(({ AllCommunityModule, ModuleRegistry }) => {
    ModuleRegistry.registerModules([AllCommunityModule]);
    return true;
  });

// Local routes. "dashboard/:code" is the same path as the target route, so the platform form (ATLAS_DASHBOARD_LINK)
// goes back to the dashboard in the same way here and at the target. At the target the current dashboard component
// stays and gets the FormType navigation (INTEGRATION.md).
export const routes: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./stand-in/shell/home.component').then((m) => m.HomeComponent) },
  {
    path: '',
    canActivateChild: [registerGridModules],
    children: [
      {
        path: 'dashboard/:code',
        loadComponent: () => import('./platform/dashboard/atlas-dashboard.component').then((m) => m.AtlasDashboardComponent),
        runGuardsAndResolvers: 'always',
      },
      { path: 'apps/:code', redirectTo: 'dashboard/:code' },
      ...atlasPlatformRoutes,
    ],
  },
  { path: '**', redirectTo: '' },
];
