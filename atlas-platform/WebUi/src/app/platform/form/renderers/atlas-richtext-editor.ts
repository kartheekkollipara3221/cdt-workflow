// COPY to the target: WebUi/src/app/platform/form/renderers/.
// The CKEditor 5 editor of a "richtext" control. Only atlas-richtext-renderer.ts loads this file, with import(),
// so the editor code is in its own chunk. The plugins come from their own packages (not from "ckeditor5"):
// an import from "ckeditor5" gives CommonJS warnings that only an angular.json change can stop.
import { Component } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { CKEditorModule } from '@ckeditor/ckeditor5-angular';
import { ClassicEditor } from '@ckeditor/ckeditor5-editor-classic';
import { Essentials } from '@ckeditor/ckeditor5-essentials';
import { Paragraph } from '@ckeditor/ckeditor5-paragraph';
import { Heading } from '@ckeditor/ckeditor5-heading';
import { Bold, Italic, Strikethrough, Underline } from '@ckeditor/ckeditor5-basic-styles';
import { List } from '@ckeditor/ckeditor5-list';
import { Link } from '@ckeditor/ckeditor5-link';
import { Table, TableToolbar } from '@ckeditor/ckeditor5-table';
import { BlockQuote } from '@ckeditor/ckeditor5-block-quote';
import { PasteFromOffice } from '@ckeditor/ckeditor5-paste-from-office';
import type { EditorConfig } from '@ckeditor/ckeditor5-core';
import { AtlasRendererBase } from './atlas-renderers';

/**
 * The editor configuration of the platform.
 *
 * It has no picture plugin and no upload adapter, so the editor drops a pasted picture. The headings are h2 to h4,
 * the same as the server allow list (AtlasRichText).
 */
export const ATLAS_RICHTEXT_CONFIG: EditorConfig = {
  licenseKey: 'GPL',
  plugins: [Essentials, Paragraph, Heading, Bold, Italic, Underline, Strikethrough, List, Link, Table, TableToolbar, BlockQuote, PasteFromOffice],
  toolbar: [
    'undo', 'redo', '|', 'heading', '|', 'bold', 'italic', 'underline', 'strikethrough', '|',
    'bulletedList', 'numberedList', '|', 'link', 'blockQuote', 'insertTable',
  ],
  heading: {
    options: [
      { model: 'paragraph', title: 'Paragraph', class: 'ck-heading_paragraph' },
      { model: 'heading2', view: 'h2', title: 'Heading 1', class: 'ck-heading_heading2' },
      { model: 'heading3', view: 'h3', title: 'Heading 2', class: 'ck-heading_heading3' },
      { model: 'heading4', view: 'h4', title: 'Heading 3', class: 'ck-heading_heading4' },
    ],
  },
  link: { defaultProtocol: 'https://' },
  table: { contentToolbar: ['tableColumn', 'tableRow', 'mergeTableCells'] },
};

/**
 * "richtext": a CKEditor 5 classic editor bound to the form control. The value is HTML.
 *
 * A disabled control shows the text in read-only mode with no toolbar. The server cleans the HTML on save.
 */
@Component({
  selector: 'atlas-richtext-editor',
  imports: [CKEditorModule, ReactiveFormsModule],
  template: `<div class="atlas-richtext" [class.atlas-richtext--readonly]="formControl.disabled" [attr.id]="'ctl_' + control.Name">
    <ckeditor [editor]="editor" [config]="config" [formControl]="formControl" />
  </div>`,
})
export class AtlasRichTextEditor extends AtlasRendererBase {
  readonly editor = ClassicEditor;
  readonly config = ATLAS_RICHTEXT_CONFIG;
}
