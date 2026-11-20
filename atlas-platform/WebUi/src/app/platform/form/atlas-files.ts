// COPY to the target: WebUi/src/app/platform/form/.
// The files of the file picker controls: the form value, the client checks and the save parts.
// The server checks the same limits again (AtlasAttachmentRules) and gives the same texts.
import { AbstractControl, FormGroup, ValidationErrors, ValidatorFn } from '@angular/forms';
import { CPApplicationControl, CPApplicationSection } from '../atlas-models';

/** The control type of a file picker. */
export const FILE_PICKER = 'filepicker';
/** The platform limit for one file, in MB. */
export const MAX_FILE_MB = 10;
/** The platform limit for the new files of one save, in MB. */
export const MAX_SAVE_MB = 25;
/** The file types that the platform accepts. A control can only make the list shorter. */
export const DEFAULT_ACCEPT: readonly string[] = ['.pdf', '.docx', '.xlsx', '.csv', '.txt', '.msg', '.png', '.jpg'];
/** The separator between the control name and the file name in the name of a file part, as the target sends it. */
export const PART_SEPARATOR = '#$#';

/** The form value of a file picker: the new files, and the IDs of the stored files that the save removes. */
export interface AtlasFileValue {
  added: File[];
  removed: string[];
}

/** One new file of a save, with the name of its file picker. */
export interface AtlasFilePart {
  control: string;
  file: File;
}

const MB = 1024 * 1024;

/** Returns true for a file picker control. */
export function isFilePicker(control: CPApplicationControl | null | undefined): boolean {
  return (control?.Type ?? '').toLowerCase() === FILE_PICKER;
}

/** Returns a value with no new and no removed files. */
export function emptyFiles(): AtlasFileValue {
  return { added: [], removed: [] };
}

/** Returns the extension of a file name in lowercase, with the dot, or an empty text. */
export function extension(name: string): string {
  const index = name.lastIndexOf('.');
  return index < 0 ? '' : name.slice(index).toLowerCase();
}

/** Returns the file types of the control: its Accept list, or the platform list. */
export function acceptOf(control: CPApplicationControl): string[] {
  const own = (control.Accept ?? []).map((x) => x.trim().toLowerCase()).filter((x) => DEFAULT_ACCEPT.includes(x));
  return control.Accept?.length ? own : [...DEFAULT_ACCEPT];
}

/** Returns the largest file of the control in MB: its MaxSizeMb, at most the platform limit. */
export function maxMbOf(control: CPApplicationControl): number {
  const mb = control.MaxSizeMb;
  return mb && mb > 0 && mb <= MAX_FILE_MB ? mb : MAX_FILE_MB;
}

/** Returns the number of files of the control after the save: the stored files that stay, and the new files. */
export function fileCount(control: CPApplicationControl, value: AtlasFileValue | null | undefined): number {
  const removed = new Set(value?.removed ?? []);
  const kept = (control.Files ?? []).filter((x) => !removed.has(String(x.ID ?? ''))).length;
  return kept + (value?.added.length ?? 0);
}

/**
 * Returns the validator of a file picker.
 *
 * The first new file that breaks a limit gives fileType, fileEmpty or fileSize. Then the count gives required
 * or maxFiles.
 *
 * @param required True when the file picker is required now. Effects can change it.
 */
export function atlasFileValidator(control: CPApplicationControl, required: boolean): ValidatorFn {
  return (item: AbstractControl): ValidationErrors | null => {
    const value = (item.value ?? emptyFiles()) as AtlasFileValue;
    const accept = acceptOf(control);
    const mb = maxMbOf(control);
    for (const file of value.added ?? []) {
      if (!accept.includes(extension(file.name))) {
        return { fileType: { file: file.name, accept } };
      }
      if (file.size === 0) {
        return { fileEmpty: { file: file.name } };
      }
      if (file.size > mb * MB) {
        return { fileSize: { file: file.name, mb } };
      }
    }
    const count = fileCount(control, value);
    const max = control.Multiple ? control.MaxFiles : 1;
    if (required && count === 0) {
      return { required: true };
    }
    if (max && max > 0 && count > max) {
      return { maxFiles: { max } };
    }
    return null;
  };
}

/** Returns the file pickers of a section: the section-level controls and the children of the containers. */
function filePickers(section: CPApplicationSection): CPApplicationControl[] {
  return (section.Controls ?? []).flatMap((x) => [x, ...(x.ChildControls ?? [])]).filter(isFilePicker);
}

/**
 * Returns the new files of the shown and enabled file pickers, for the save.
 *
 * A hidden or disabled file picker sends nothing, as a field.
 */
export function fileParts(section: CPApplicationSection, group: FormGroup, shown: (name: string) => boolean): AtlasFilePart[] {
  return filePickers(section)
    .filter((x) => shown(x.Name) && group.get(x.Name)?.enabled)
    .flatMap((x) => ((group.get(x.Name)?.value as AtlasFileValue | null)?.added ?? []).map((file) => ({ control: x.Name, file })));
}

/** Returns the IDs of the stored files that the shown and enabled file pickers remove. */
export function removedFileIds(section: CPApplicationSection, group: FormGroup, shown: (name: string) => boolean): number[] {
  return filePickers(section)
    .filter((x) => shown(x.Name) && group.get(x.Name)?.enabled)
    .flatMap((x) => (group.get(x.Name)?.value as AtlasFileValue | null)?.removed ?? [])
    .map((x) => Number(x))
    .filter((x) => Number.isInteger(x) && x > 0);
}

/** Returns true when the new files of a save are more than the platform limit for one save. */
export function tooLargeForOneSave(parts: AtlasFilePart[]): boolean {
  return parts.reduce((sum, x) => sum + x.file.size, 0) > MAX_SAVE_MB * MB;
}

/** The text for too many MB in one save, the same as the server text. */
export const SAVE_SIZE_TEXT = `Save at most ${MAX_SAVE_MB} MB at one time.`;
