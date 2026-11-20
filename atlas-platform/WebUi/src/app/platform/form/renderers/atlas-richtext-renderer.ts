// COPY to the target: WebUi/src/app/platform/form/renderers/.
// "richtext": loads the editor (atlas-richtext-editor.ts) with import() the first time a form needs it.
// Thus a form with no rich text control does not load the editor code.
import { NgComponentOutlet } from '@angular/common';
import { Component, OnInit, Type, signal } from '@angular/core';
import { LoaderComponent } from '../../atlas-pbdev';
import { AtlasRendererBase } from './atlas-renderers';

let editorType: Promise<Type<unknown>> | null = null;

/** Returns the editor component. The code loads one time for the application. */
export function loadRichTextEditor(): Promise<Type<unknown>> {
  editorType ??= import('./atlas-richtext-editor').then((m) => m.AtlasRichTextEditor);
  return editorType;
}

/**
 * "richtext": shows a loader, then the editor with the same control and form group.
 *
 * When the editor code cannot load, the control shows a message, and the form cannot change the value.
 */
@Component({
  selector: 'atlas-richtext-renderer',
  imports: [NgComponentOutlet, LoaderComponent],
  template: `@if (editor(); as type) {
      <ng-container *ngComponentOutlet="type; inputs: { control: control, group: group, appCode: appCode }" />
    } @else if (failed()) {
      <div class="atlas-unsupported" role="alert">The text editor cannot open. Reload the page.</div>
    } @else {
      <pbdev-loader variant="loader-1" [widthRem]="1.5" />
    }`,
})
export class AtlasRichTextRenderer extends AtlasRendererBase implements OnInit {
  /** The editor component when it is loaded. */
  readonly editor = signal<Type<unknown> | null>(null);
  /** True when the editor code did not load. */
  readonly failed = signal(false);

  ngOnInit(): void {
    loadRichTextEditor().then(
      (type) => this.editor.set(type),
      () => {
        this.failed.set(true);
        this.formControl?.disable({ emitEvent: false });
      },
    );
  }
}
