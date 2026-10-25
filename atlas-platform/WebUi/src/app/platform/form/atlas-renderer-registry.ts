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

  /** Types that render no input with the id "ctl_" + Name, so the form label gets no "for" attribute. */
  private readonly noInput = new Set<string>(['user', 'grid']);

  register(type: string, component: Type<unknown>, hasInput = true): void {
    const key = type.toLowerCase();
    this.renderers.set(key, component);
    if (hasInput) {
      this.noInput.delete(key);
    } else {
      this.noInput.add(key);
    }
  }

  /** True when the renderer of the type has an input that a label can point to. */
  hasInput(type: string | null | undefined): boolean {
    return !this.noInput.has((type ?? '').toLowerCase());
  }

  get(type: string | null | undefined): Type<unknown> | null {
    return this.renderers.get((type ?? '').toLowerCase()) ?? null;
  }

  has(type: string | null | undefined): boolean {
    return this.get(type) !== null;
  }
}
