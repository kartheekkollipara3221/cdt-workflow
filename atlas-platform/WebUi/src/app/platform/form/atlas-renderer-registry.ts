// COPY to the target: WebUi/src/app/platform/form/. The renderer registry (spec 01, 5.5): control Type to
// component. A new control type is one provideAtlasRenderers entry (or register() call); the form does not change.
import { EnvironmentProviders, Injectable, Type, inject, provideEnvironmentInitializer } from '@angular/core';
import {
  AtlasAmountRenderer,
  AtlasCheckboxRenderer,
  AtlasRadioRenderer,
  AtlasRendererBase,
  AtlasUnsupportedRenderer,
  AtlasDateRenderer,
  AtlasNgSelectRenderer,
  AtlasSelectRenderer,
  AtlasTextRenderer,
  AtlasTextareaRenderer,
  AtlasUserRenderer,
} from './renderers/atlas-renderers';
import { AtlasGridRenderer } from './renderers/atlas-grid-renderer';

@Injectable({ providedIn: 'root' })
export class AtlasRendererRegistry {
  private readonly renderers = new Map<string, Type<unknown>>([
    ['text', AtlasTextRenderer],
    ['currency', AtlasAmountRenderer],
    ['percent', AtlasAmountRenderer],
    ['select', AtlasSelectRenderer],
    ['ngselect', AtlasNgSelectRenderer],
    ['textarea', AtlasTextareaRenderer],
    ['datetimepicker', AtlasDateRenderer],
    ['date', AtlasDateRenderer],
    ['checkbox', AtlasCheckboxRenderer],
    ['check', AtlasCheckboxRenderer],
    ['option', AtlasRadioRenderer],
    ['user', AtlasUserRenderer],
    ['grid', AtlasGridRenderer],
  ]);

  /** Types that render no input with the id "ctl_" + Name, so the form label gets no "for" attribute. */
  // A checkbox has its own (empty) label inside the library control; a second label "for" it gives two labels
  // (review NG-08), so the form uses aria-labelledby for it.
  private readonly noInput = new Set<string>(['user', 'grid', 'checkbox', 'check']);

  /** Types whose pbdev control draws its own error text (pbdev-input-messages); the form does not draw it again. */
  private readonly ownMessages = new Set<string>(['text', 'currency', 'percent', 'select', 'textarea', 'datetimepicker', 'date']);

  /** True when the control of the type shows its error text itself (review R2, decision D2). */
  drawsMessages(type: string | null | undefined): boolean {
    return this.ownMessages.has((type ?? '').toLowerCase());
  }

  register(type: string, component: Type<unknown>, hasInput = true): void {
    const key = type.toLowerCase();
    this.renderers.set(key, component);
    this.ownMessages.delete(key);
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

  /** The registered types (for example for a test that renders each type). */
  types(): string[] {
    return [...this.renderers.keys()];
  }

  /** The renderer of the type, or the "unsupported" note for a type with no renderer (review NG-04). */
  resolve(type: string | null | undefined): Type<unknown> {
    return this.get(type) ?? AtlasUnsupportedRenderer;
  }
}

/**
 * Adds renderers of an app (review NG-04), for example in app.config.ts:
 * provideAtlasRenderers({ rating: RatingRenderer }). A renderer extends AtlasRendererBase.
 * Types in noInput have no input for the label (as "user" and "grid").
 */
export function provideAtlasRenderers(
  renderers: Record<string, Type<AtlasRendererBase>>,
  noInput: readonly string[] = [],
): EnvironmentProviders {
  return provideEnvironmentInitializer(() => {
    const registry = inject(AtlasRendererRegistry);
    for (const [type, component] of Object.entries(renderers)) {
      registry.register(type, component, !noInput.includes(type));
    }
  });
}
