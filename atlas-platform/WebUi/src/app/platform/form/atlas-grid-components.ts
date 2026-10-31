// COPY to the target: WebUi/src/app/platform/form/. The cell components of the "grid" control (review R4), by the
// cellRenderer name in the JSON - the frameworkComponents of cp-atlas-sectionmaster.ts. A small file with no ag-grid
// import, so app.config.ts can use it and ag-grid stays in the lazy chunks.
import { InjectionToken, Provider, Type } from '@angular/core';

export const ATLAS_GRID_COMPONENTS = new InjectionToken<Record<string, Type<unknown>>>('ATLAS_GRID_COMPONENTS', {
  factory: () => ({}),
});

/** At the target, in app.config.ts: provideAtlasGridComponents({ TextEditor, SelectEditor, NumericEditor }). */
export function provideAtlasGridComponents(components: Record<string, Type<unknown>>): Provider {
  return { provide: ATLAS_GRID_COMPONENTS, useValue: components };
}
