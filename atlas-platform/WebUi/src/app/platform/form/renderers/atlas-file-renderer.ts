// COPY to the target: WebUi/src/app/platform/form/renderers/.
// "filepicker": the pbdev file picker for the new files, and the list of the stored files, as the CDT step page.
import { Component, inject } from '@angular/core';
import { DialogComponent, FilePickerCoreComponent } from '../../atlas-pbdev';
import { AtlasApiService } from '../../atlas-api.service';
import { AtlasNoticeService } from '../../atlas-notice.service';
import { problemText } from '../../atlas-errors';
import { CPAttachment } from '../../atlas-models';
import { AtlasFileValue, acceptOf, emptyFiles } from '../atlas-files';
import { AtlasRendererBase } from './atlas-renderers';

/**
 * "filepicker": adds new files, removes stored files and downloads stored files.
 *
 * The form value holds the new files and the IDs of the removed files. The save sends them. A removed file shows as
 * "removed when you save"; nothing is removed when the user closes the form with no save. A disabled file picker
 * shows the stored files with the download action only.
 */
@Component({
  selector: 'atlas-file-renderer',
  imports: [FilePickerCoreComponent, DialogComponent],
  template: `<div class="atlas-files" [attr.id]="'ctl_' + control.Name">
    @if (!disabled) {
      <pbdev-file-picker-core [label]="control.PlaceHolder || null" buttonText="Upload" [multiple]="!!control.Multiple"
        [accept]="acceptText" (filesSelect)="add($event)" (filesDrop)="add($event)" (touched)="formControl.markAsTouched()" />
    }
    @for (file of control.Files ?? []; track file.ID) {
      <div class="link-button-container" [attr.id]="'divA_' + file.ID">
        <a href="" (click)="download(file, $event)">{{ file.Name }}</a>
        @if (isRemoved(file)) {
          <span class="atlas-file-note">removed when you save</span>
        } @else if (!disabled && !file.Disabled) {
          <button type="button" class="btn-active" [attr.id]="'btn_RemoveFile_' + file.ID" [attr.aria-label]="'Remove ' + file.Name"
            (click)="asking = file"><i class="pbdev-icon-trash"></i></button>
        }
      </div>
    }
    @for (file of value.added; track $index) {
      <div class="link-button-container atlas-file-new">
        <span>{{ file.name }}</span>
        @if (!disabled) {
          <button type="button" class="btn-active" [attr.aria-label]="'Remove ' + file.name" (click)="removeNew($index)">
            <i class="pbdev-icon-trash"></i>
          </button>
        }
      </div>
    }
    <pbdev-dialog [visible]="asking !== null" (visibleChange)="asking = null">
      <div class="modal-confirm-body">Are you sure you want to delete the file?</div>
      <div class="modal-confirm-footer">
        <button type="button" class="modal-confirm-no-btn" (click)="asking = null">No</button>
        <button type="button" class="modal-confirm-yes-btn" [attr.id]="'btn_ConfirmRemoveFile_' + control.Name" (click)="confirmRemove()">Yes</button>
      </div>
    </pbdev-dialog>
  </div>`,
})
export class AtlasFileRenderer extends AtlasRendererBase {
  private readonly api = inject(AtlasApiService);
  private readonly notice = inject(AtlasNoticeService);

  /** The stored file that the remove question is for, or null. */
  asking: CPAttachment | null = null;

  /** True when the user cannot change the files. */
  get disabled(): boolean {
    return this.formControl.disabled;
  }

  /** The new files and the removed file IDs. */
  get value(): AtlasFileValue {
    return (this.formControl.value as AtlasFileValue | null) ?? emptyFiles();
  }

  /** The accept text of the file input, for example ".pdf,.msg". */
  get acceptText(): string {
    return acceptOf(this.control).join(',');
  }

  /** Returns true when the save removes the stored file. */
  isRemoved(file: CPAttachment): boolean {
    return this.value.removed.includes(String(file.ID ?? ''));
  }

  /** Adds the selected files. A file picker with no Multiple keeps only the last file. */
  add(files: File[]): void {
    const value = this.value;
    const added = this.control.Multiple ? [...value.added, ...files] : files.slice(-1);
    this.update({ added, removed: value.removed });
  }

  /** Removes a new file from the selection. */
  removeNew(index: number): void {
    const value = this.value;
    this.update({ added: value.added.filter((_, i) => i !== index), removed: value.removed });
  }

  /** Marks the stored file of the question as removed. The save removes it. */
  confirmRemove(): void {
    const file = this.asking;
    this.asking = null;
    if (file?.ID && !this.isRemoved(file)) {
      this.update({ added: this.value.added, removed: [...this.value.removed, String(file.ID)] });
    }
  }

  /** Gets the stored file through the API and gives it to the browser as a download. */
  download(file: CPAttachment, event: Event): void {
    event.preventDefault();
    if (!file.ID) {
      return;
    }
    this.api.download(this.appCode, file.ID).subscribe({
      next: (response) => saveBlob(response.body, file.Name ?? 'file'),
      error: (error) => this.notice.show('error', problemText(error)),
    });
  }

  private update(value: AtlasFileValue): void {
    this.formControl.setValue(value);
    this.formControl.markAsTouched();
    this.formControl.markAsDirty();
  }
}

/** Gives a blob to the browser as a download with the file name. */
function saveBlob(blob: Blob | null, name: string): void {
  if (!blob) {
    return;
  }
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  document.body.appendChild(link);
  link.click();
  link.remove();
  // The browser starts the download in the click. Free the blob after that.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}
