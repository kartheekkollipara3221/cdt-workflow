// COPY to the target: WebUi/Source/App/platform/form/. The renderer registry (spec 01, 5.5): control Type to
// component. A new control type is one register() call; the form component does not change.
import { Injectable, Type } from '@angular/core';
import {
  AtlasCheckboxRenderer,
  AtlasDateRenderer,
  AtlasGridRowRenderer,
  AtlasNgSelectRenderer,
  AtlasSelectRenderer,
  AtlasTextRenderer,
  AtlasTextareaRenderer,
  AtlasUserRenderer,
} from './renderers/atlas-renderers';

@Injectable({ providedIn: 'root' })
export class AtlasRendererRegistry {
  private readonly renderers = new Map<string, Type<unknown>>([
    ['text', AtlasTextRenderer],
    ['currency', AtlasTextRenderer],
    ['percent', AtlasTextRenderer],
    ['select', AtlasSelectRenderer],
    ['ngselect', AtlasNgSelectRenderer],
    ['textarea', AtlasTextareaRenderer],
    ['datetimepicker', AtlasDateRenderer],
    ['date', AtlasDateRenderer],
    ['checkbox', AtlasCheckboxRenderer],
    ['user', AtlasUserRenderer],
    ['grid', AtlasGridRowRenderer],
  ]);

  register(type: string, component: Type<unknown>): void {
    this.renderers.set(type.toLowerCase(), component);
  }

  get(type: string | null | undefined): Type<unknown> | null {
    return this.renderers.get((type ?? '').toLowerCase()) ?? null;
  }

  has(type: string | null | undefined): boolean {
    return this.get(type) !== null;
  }
}
