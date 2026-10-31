// COPY to the target: WebUi/src/app/platform/form/. Review NG-08: the accessible name, state and error of the input
// inside a renderer. It works on the rendered DOM, so it does not need inputs of the @pbdev controls.
import { AfterViewChecked, Directive, ElementRef, inject, input } from '@angular/core';

@Directive({ selector: '[atlasFieldA11y]' })
export class AtlasFieldA11yDirective implements AfterViewChecked {
  /** The control name: the error has the id "err_" + name, the label text has the id "lbl_" + name. */
  readonly atlasFieldA11y = input.required<string>();
  readonly required = input(false);
  readonly invalid = input(false);
  /** True when the label has no "for" (for example a checkbox): the input gets aria-labelledby. */
  readonly labelledBy = input(false);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  ngAfterViewChecked(): void {
    const element = this.host.nativeElement.querySelector<HTMLElement>('input, select, textarea');
    if (!element) {
      return;
    }
    const name = this.atlasFieldA11y();
    set(element, 'aria-required', this.required() ? 'true' : null);
    set(element, 'aria-invalid', this.invalid() ? 'true' : null);
    set(element, 'aria-describedby', this.invalid() ? 'err_' + name : null);
    set(element, 'aria-labelledby', this.labelledBy() ? 'lbl_' + name : null);
  }
}

function set(element: HTMLElement, name: string, value: string | null): void {
  if (value === null) {
    if (element.hasAttribute(name)) {
      element.removeAttribute(name);
    }
  } else if (element.getAttribute(name) !== value) {
    element.setAttribute(name, value);
  }
}
