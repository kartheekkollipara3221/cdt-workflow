import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withInMemoryScrolling, withRouterConfig } from '@angular/router';

import { provideToastr } from 'ngx-toastr';

import { routes } from './app.routes';
import { provideAtlasGridComponents } from './platform/form/atlas-grid-components';
import { NumericEditor, SelectEditor, TextEditor } from './stand-in/cell-editors/cell-editors';
import { lookProviders } from './stand-in/shell/look.providers';
import { standInInterceptor } from './stand-in/shell/stand-in.interceptor';

/** Providers of the local stand-in app: router, HTTP client, toastr, look and grid cell editors. */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withRouterConfig({ onSameUrlNavigation: 'reload' }), withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideHttpClient(withInterceptors([standInInterceptor])),
    // The same toastr options as the target app.config.ts.
    provideToastr({
      tapToDismiss: true,
      positionClass: 'toast-bottom-right',
      timeOut: 5000,
      titleClass: 'toast-title',
      messageClass: 'toast-message',
    }),
    ...lookProviders,
    // At the target: the components of Shared/Component/Cell-editors.
    provideAtlasGridComponents({ TextEditor, SelectEditor, NumericEditor }),
  ],
};
