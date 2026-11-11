// COPY to the target: WebUi/src/app/platform/. It uses ngx-toastr, as the section master does
// (cp-atlas-sectionmaster.ts onSave: toastr.info("Record saved successfully", "Info")).
// The target app already provides toastr (provideToastr) and styles it (styles.scss .toast-container).
import { Injectable, inject } from '@angular/core';
import { ToastrService } from 'ngx-toastr';

/** Shows a short message (toast) after a save or a delete. */
@Injectable({ providedIn: 'root' })
export class AtlasNoticeService {
  private readonly toastr = inject(ToastrService);

  /** Shows the text as an error toast or as an info toast. */
  show(kind: 'success' | 'error', text: string): void {
    if (kind === 'error') {
      this.toastr.error(text, 'Error');
    } else {
      this.toastr.info(text, 'Info');
    }
  }
}
