// COPY to the target: WebUi/src/app/platform/form/.
// This file has no ag-grid import. Thus app.config.ts can use it, and ag-grid stays in the lazy chunks.
import { InjectionToken, Provider, Type } from '@angular/core';

/**
 * The cell components of the "grid" control, by the cellRenderer name in the JSON.
 *
 * They are the same as the frameworkComponents of cp-atlas-sectionmaster.ts.
 */
export const ATLAS_GRID_COMPONENTS = new InjectionToken<Record<string, Type<unknown>>>('ATLAS_GRID_COMPONENTS', {
  factory: () => ({}),
});

/**
 * Provides the cell components of the "grid" control.
 *
 * At the target, in app.config.ts: provideAtlasGridComponents({ TextEditor, SelectEditor, NumericEditor }).
 */
export function provideAtlasGridComponents(components: Record<string, Type<unknown>>): Provider {
  return { provide: ATLAS_GRID_COMPONENTS, useValue: components };
}
