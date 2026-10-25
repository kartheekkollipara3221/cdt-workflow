// COPY to the target: WebUi/Source/App/platform/. A short message after a save or a delete. At the target the
// shell can show it with ngx-toastr; locally the stand-in shell shows it in a banner.
import { Injectable, signal } from '@angular/core';

export interface AtlasNotice {
  kind: 'success' | 'error';
  text: string;
}

@Injectable({ providedIn: 'root' })
export class AtlasNoticeService {
  readonly current = signal<AtlasNotice | null>(null);
  private timer: ReturnType<typeof setTimeout> | null = null;

  show(kind: AtlasNotice['kind'], text: string): void {
    this.current.set({ kind, text });
    if (this.timer) {
      clearTimeout(this.timer);
    }
    this.timer = setTimeout(() => this.current.set(null), 5000);
  }

  clear(): void {
    this.current.set(null);
  }
}
