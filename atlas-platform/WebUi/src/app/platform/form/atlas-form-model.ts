// COPY to the target: WebUi/src/app/platform/form/. The form model of a section: the form group, the outputs
// for the save, and the client value of a Computed field. Plain functions, so the unit tests need no component.
import { FormControl, FormGroup, FormRecord } from '@angular/forms';
import { CPApplicationControl, CPApplicationSection, CPPageOutput } from '../atlas-models';
import { isNil, parseLocalDay } from '../atlas-dates';
import { atlasValidators } from './atlas-validators';
import { atlasFileValidator, emptyFiles, isFilePicker } from './atlas-files';

/** The grid BindMode that binds one grid row to form fields. */
export const SINGLE_ROW_FIELDS = 'SingleRowFields';

/** Returns true when the control is a "section" container. */
export function isSection(control: CPApplicationControl): boolean {
  return (control.Type ?? '').toLowerCase() === 'section';
}

/** Returns true when the control is a grid with the BindMode SingleRowFields. */
export function isSingleRowGrid(control: CPApplicationControl): boolean {
  return (control.Grid?.BindMode ?? '').toLowerCase() === SINGLE_ROW_FIELDS.toLowerCase();
}

/**
 * The value kind of a control.
 *
 * pbdev-date-picker holds a JS Date. pbdev-checkbox holds a boolean. A multiple ng-select holds a string list.
 * The server keeps text: yyyy-MM-dd, 'true' or 'false', and a list joined with '#$#'.
 * The list format is the same as in cp-atlas-sectionmaster.ts.
 */
export type AtlasValueKind = 'text' | 'date' | 'bool' | 'list';

/** The separator of the items of a list value in the server text. */
export const LIST_SEPARATOR = '#$#';

/** Returns the value kind of a control from its Type and Multiple. */
export function valueKind(control: CPApplicationControl): AtlasValueKind {
  const type = (control.Type ?? '').toLowerCase();
  if (type === 'datetimepicker' || type === 'date') {
    return 'date';
  }
  if (type === 'check' || type === 'checkbox') {
    return 'bool';
  }
  if (type === 'ngselect' && control.Multiple) {
    return 'list';
  }
  return 'text';
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

/**
 * Converts a server text to the form value of the control.
 *
 * The text can come from the JSON Value, a Set effect or a server response.
 */
export function toModelValue(control: CPApplicationControl, text: string | null | undefined): unknown {
  switch (valueKind(control)) {
    case 'date':
      return parseLocalDay((text ?? '').trim());
    case 'bool':
      return ['true', 'yes', '1'].includes((text ?? '').trim().toLowerCase());
    case 'list':
      return text ? text.split(LIST_SEPARATOR) : [];
    default:
      return text ?? '';
  }
}

/**
 * Converts a form value to the server text.
 *
 * Returns null for an empty value. An empty checkbox gives 'false'.
 */
export function toOutputValue(control: CPApplicationControl, value: unknown): string | null {
  const kind = valueKind(control);
  if (isNil(value)) {
    return kind === 'bool' ? 'false' : null;
  }
  switch (kind) {
    case 'date':
      if (value instanceof Date) {
        return Number.isNaN(value.getTime()) ? null : `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
      }
      break;
    case 'bool':
      if (typeof value === 'boolean') {
        return value ? 'true' : 'false';
      }
      break;
    case 'list':
      if (Array.isArray(value)) {
        return value.length ? value.map((x) => String(x)).join(LIST_SEPARATOR) : null;
      }
      break;
  }
  return value === '' ? null : String(value);
}

/**
 * Returns the visible and required flags of the controls from the JSON.
 *
 * The effects change these flags later.
 */
export function initialFlags(section: CPApplicationSection): { visible: Record<string, boolean>; required: Record<string, boolean> } {
  const visible: Record<string, boolean> = {};
  const required: Record<string, boolean> = {};
  for (const control of section.Controls ?? []) {
    visible[control.Name] = control.Visible !== false;
    for (const child of control.ChildControls ?? []) {
      visible[child.Name] = child.Visible !== false;
      required[child.Name] = !!child.Validator?.Required;
    }
  }
  return { visible, required };
}

/** Returns the value controls of a section: the section-level controls and the children of "section" containers. */
export function valueControls(section: CPApplicationSection): CPApplicationControl[] {
  const list: CPApplicationControl[] = [];
  for (const control of section.Controls ?? []) {
    if (isSection(control)) {
      list.push(...(control.ChildControls ?? []));
    } else {
      list.push(control);
    }
  }
  return list;
}

/** Returns the "section" container of each child control, as a map from child name to section name. */
export function containerOf(section: CPApplicationSection): Map<string, string> {
  const map = new Map<string, string>();
  for (const control of section.Controls ?? []) {
    if (isSection(control)) {
      for (const child of control.ChildControls ?? []) {
        map.set(child.Name, control.Name);
      }
    }
  }
  return map;
}

/**
 * Returns true when the control and its section are visible.
 *
 * The server validates and accepts only these fields. Thus a hidden field, or a field of a hidden section,
 * does not stop the save and is not sent.
 */
export function isShown(name: string, containers: Map<string, string>, visible: (name: string) => boolean): boolean {
  const container = containers.get(name);
  return visible(name) && (container === undefined || visible(container));
}

/** Returns true when a shown and enabled control (or grid cell) is not valid. */
export function shownInvalid(section: CPApplicationSection, group: FormGroup, shown: (name: string) => boolean): boolean {
  return valueControls(section).some((control) => {
    const item = group.get(control.Name);
    return shown(control.Name) && !!item && item.enabled && item.invalid;
  });
}

/** The form of a section. It has dynamic keys, so it is a FormRecord. A FormRecord is a FormGroup. */
export type AtlasFormRecord = FormRecord<FormControl | FormRecord<FormControl>>;

/**
 * Builds the form of a section.
 *
 * Makes one form control for each value control, and a nested group for each SingleRowFields grid.
 */
export function buildFormGroup(section: CPApplicationSection): AtlasFormRecord {
  const group: AtlasFormRecord = new FormRecord<FormControl | FormRecord<FormControl>>({});
  for (const control of valueControls(section)) {
    if (isSingleRowGrid(control)) {
      const row = control.Grid?.Data?.[0] ?? {};
      const rowGroup = new FormRecord<FormControl>({});
      for (const column of control.Grid?.Columns ?? []) {
        if (!column.field) {
          continue;
        }
        const disabled = !!control.Disabled || !!column.cellRendererParams?.disabled;
        rowGroup.addControl(
          column.field,
          new FormControl<string | null>(
            { value: row[column.field] ?? '', disabled },
            disabled ? [] : atlasValidators(control.Validator, false),
          ),
        );
      }
      group.addControl(control.Name, rowGroup);
    } else if (control.Grid) {
      continue; // Release P1 supports only SingleRowFields grids.
    } else if (isFilePicker(control)) {
      // The value holds the new files and the removed file IDs. The stored files are in control.Files.
      group.addControl(
        control.Name,
        new FormControl<unknown>({ value: emptyFiles(), disabled: !!control.Disabled }, atlasFileValidator(control, !!control.Validator?.Required)),
      );
    } else {
      group.addControl(
        control.Name,
        new FormControl<unknown>(
          { value: toModelValue(control, control.Value), disabled: !!control.Disabled },
          atlasValidators(control.Validator, !!control.Validator?.Required),
        ),
      );
    }
  }
  return group;
}

/**
 * Builds the save outputs of a section.
 *
 * Contains only the visible and enabled fields. A SingleRowFields grid gives one output.
 * Its value is a JSON list of one row with the enabled cells, as the current client sends it.
 * A file picker gives no output: its files go as file parts (see atlas-files.ts).
 */
export function buildOutputs(section: CPApplicationSection, group: FormGroup, visible: (name: string) => boolean): CPPageOutput[] {
  const outputs: CPPageOutput[] = [];
  for (const control of valueControls(section)) {
    if (!visible(control.Name)) {
      continue;
    }
    const item = group.get(control.Name);
    if (!item || item.disabled || isFilePicker(control)) {
      continue;
    }
    if (item instanceof FormGroup) {
      const row: Record<string, string> = {};
      for (const [field, cell] of Object.entries(item.controls)) {
        if (cell.enabled) {
          row[field] = isNil(cell.value) ? '' : String(cell.value).trim();
        }
      }
      outputs.push({ Name: control.Name, Value: JSON.stringify([row]) });
    } else {
      outputs.push({ Name: control.Name, Value: toOutputValue(control, item.value) });
    }
  }
  return outputs;
}

/**
 * Calculates the client value of a Computed field (Sum, Count, Min, Max).
 *
 * Empty values are not used, as on the server (AtlasComputedFields). The shared cases are in
 * atlas-effect-cases.ts. Returns null when the expression is not known.
 */
export function computeValue(expression: string, values: Record<string, unknown>): number | null {
  const match = /^\s*(Sum|Count|Min|Max)\s*\((.*)\)\s*$/i.exec(expression);
  if (!match) {
    return null;
  }
  const numbers = match[2]
    .split(',')
    .map((x) => x.trim())
    .filter((x) => x.length > 0)
    .map((name) => values[name])
    .filter((x) => !isNil(x) && String(x).trim() !== '' && !Number.isNaN(Number(x)))
    .map((x) => Number(x));
  switch (match[1].toLowerCase()) {
    case 'sum':
      return numbers.reduce((a, b) => a + b, 0);
    case 'count':
      return numbers.length;
    case 'min':
      return numbers.length ? Math.min(...numbers) : null;
    default:
      return numbers.length ? Math.max(...numbers) : null;
  }
}
