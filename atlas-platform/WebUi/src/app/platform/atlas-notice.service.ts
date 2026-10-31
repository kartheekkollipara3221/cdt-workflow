// COPY to the target: WebUi/src/app/platform/. A short message after a save or a delete. As the section master
// (cp-atlas-sectionmaster.ts onSave: toastr.info("Record saved successfully", "Info")), it uses ngx-toastr, which the
// target app already provides (provideToastr) and styles (styles.scss .toast-container). Review R3.
import { Injectable, inject } from '@angular/core';
import { ToastrService } from 'ngx-toastr';

@Injectable({ providedIn: 'root' })
export class AtlasNoticeService {
  private readonly toastr = inject(ToastrService);

  show(kind: 'success' | 'error', text: string): void {
    if (kind === 'error') {
      this.toastr.error(text, 'Error');
    } else {
      this.toastr.info(text, 'Info');
    }
  }
}
