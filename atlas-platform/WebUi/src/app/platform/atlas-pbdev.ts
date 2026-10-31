// COPY to the target: WebUi/src/app/platform/. The only file of the platform that imports @pbdev/pbdev-core.
// The platform uses only the classes and inputs that the target WebUi already uses (evidence: atlas-platform/PBDEV.md).
// If the real library differs, change this file and the bindings listed in PBDEV.md section 3.
import { AfterViewInit, Directive, ElementRef, Input, inject } from '@angular/core';
import { CPKeyValue } from './atlas-models';

export {
  AgLoadingOverlayComponent,
  CheckboxComponent,
  DatePickerComponent,
  DialogComponent,
  InputComponent,
  LoaderComponent,
  SectionComponent,
  SelectComponent,
  TextareaComponent,
} from '@pbdev/pbdev-core';
export type { SelectOption } from '@pbdev/pbdev-core';

/** The target maps CPKeyValue to the label/value objects of pbdev-select (cp-atlas-sectionmaster.ts transformSelect). */
export function toSelectOptions(items: readonly CPKeyValue[] | null | undefined): { label: string; value: string }[] {
  return (items ?? []).map((x) => ({ label: x.Label ?? x.Value ?? '', value: x.Value ?? '' }));
}

/**
 * Puts the id (and the name) on the first input, select or textarea inside a library control, so the form label
 * (for="ctl_Name") points to it. It does not depend on an id input of the library control.
 */
@Directive({ selector: '[atlasControlId]' })
export class AtlasControlIdDirective implements AfterViewInit {
  @Input({ required: true }) atlasControlId!: string;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  ngAfterViewInit(): void {
    const element = this.host.nativeElement.querySelector<HTMLElement>('input, select, textarea');
    if (!element) {
      return;
    }
    element.id = this.atlasControlId;
    if (!element.getAttribute('name')) {
      element.setAttribute('name', this.atlasControlId);
    }
  }
}
