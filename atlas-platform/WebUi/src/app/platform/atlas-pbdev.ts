// COPY to the target: WebUi/src/app/platform/. The only file of the platform that imports @pbdev/pbdev-core.
// The platform uses only the classes and inputs that the target WebUi already uses.
// If the real library is different, change this file and the platform bindings of these controls.
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

/**
 * Converts the options of a control to the label/value objects of pbdev-select.
 *
 * The target does the same in cp-atlas-sectionmaster.ts transformSelect. A missing label gets the value.
 */
export function toSelectOptions(items: readonly CPKeyValue[] | null | undefined): { label: string; value: string }[] {
  return (items ?? []).map((x) => ({ label: x.Label ?? x.Value ?? '', value: x.Value ?? '' }));
}

/**
 * Puts the id (and the name) on the first input, select or textarea inside a library control.
 *
 * Thus the form label (for="ctl_Name") points to it. It does not need an id input on the library control.
 * It keeps a name that the element has.
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
