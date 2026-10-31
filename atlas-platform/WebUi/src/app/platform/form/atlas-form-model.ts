// COPY to the target: WebUi/src/app/platform/form/. The form model of a section: the form group, the outputs
// for the save, and the client value of a Computed field. Plain functions, so the unit tests need no component.
import { FormControl, FormGroup, FormRecord } from '@angular/forms';
import { CPApplicationControl, CPApplicationSection, CPPageOutput } from '../atlas-models';
import { atlasValidators } from './atlas-validators';

export const SINGLE_ROW_FIELDS = 'SingleRowFields';

export function isSection(control: CPApplicationControl): boolean {
  return (control.Type ?? '').toLowerCase() === 'section';
}

export function isSingleRowGrid(control: CPApplicationControl): boolean {
  return (control.Grid?.BindMode ?? '').toLowerCase() === SINGLE_ROW_FIELDS.toLowerCase();
}

/**
 * The value kind of a control (review R2, real pbdev-core): pbdev-date-picker holds a JS Date, pbdev-checkbox a
 * boolean, a multiple ng-select a string list. The server keeps text: yyyy-MM-dd, 'true'/'false', and a list
 * joined with '#$#' (as cp-atlas-sectionmaster.ts).
 */
export type AtlasValueKind = 'text' | 'date' | 'bool' | 'list';

export const LIST_SEPARATOR = '#$#';

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

/** The form value of a server text (the JSON Value, a Set effect, a server answer). */
export function toModelValue(control: CPApplicationControl, text: string | null | undefined): unknown {
  switch (valueKind(control)) {
    case 'date': {
      const match = /^(\d{4})-(\d{2})-(\d{2})/.exec((text ?? '').trim());
      return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
    }
    case 'bool':
      return ['true', 'yes', '1'].includes((text ?? '').trim().toLowerCase());
    case 'list':
      return text ? text.split(LIST_SEPARATOR) : [];
    default:
      return text ?? '';
  }
}

/** The server text of a form value (null when empty). */
export function toOutputValue(control: CPApplicationControl, value: unknown): string | null {
  if (value === null || value === undefined) {
    return valueKind(control) === 'bool' ? 'false' : null;
  }
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
  }
  if (typeof value === 'boolean') {
    return value ? 'true' : 'false';
  }
  if (Array.isArray(value)) {
    return value.length ? value.map((x) => String(x)).join(LIST_SEPARATOR) : null;
  }
  return value === '' ? null : String(value);
}

/** The value controls of a section: the section-level controls and the children of "section" containers. */
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

/** The section container of each child control (child name to section name). */
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
 * True if the control and its section are visible (review NG-03). The server validates and accepts only these
 * fields, so a hidden field or a field of a hidden section does not stop the save and is not sent.
 */
export function isShown(name: string, containers: Map<string, string>, visible: (name: string) => boolean): boolean {
  const container = containers.get(name);
  return visible(name) && (container === undefined || visible(container));
}

/** True if a shown and enabled control (or grid cell) is not valid. */
export function shownInvalid(section: CPApplicationSection, group: FormGroup, shown: (name: string) => boolean): boolean {
  return valueControls(section).some((control) => {
    const item = group.get(control.Name);
    return shown(control.Name) && !!item && item.enabled && item.invalid;
  });
}

/** The form of a section: dynamic keys, so a FormRecord (review NG-11). A FormRecord is a FormGroup. */
export type AtlasFormRecord = FormRecord<FormControl | FormRecord<FormControl>>;

/** One form control for each value control; a nested group for each SingleRowFields grid. */
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
      continue; // other grids are not in P1
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
 * The save outputs (spec 01, 5.9 step 4): only visible and enabled fields. A SingleRowFields grid is sent as one
 * output with a JSON list of one row (the enabled cells), as the current client does.
 */
export function buildOutputs(section: CPApplicationSection, group: FormGroup, visible: (name: string) => boolean): CPPageOutput[] {
  const outputs: CPPageOutput[] = [];
  for (const control of valueControls(section)) {
    if (!visible(control.Name)) {
      continue;
    }
    const item = group.get(control.Name);
    if (!item || item.disabled) {
      continue;
    }
    if (item instanceof FormGroup) {
      const row: Record<string, string> = {};
      for (const [field, cell] of Object.entries(item.controls)) {
        if (cell.enabled) {
          row[field] = cell.value === null || cell.value === undefined ? '' : String(cell.value).trim();
        }
      }
      outputs.push({ Name: control.Name, Value: JSON.stringify([row]) });
    } else {
      outputs.push({ Name: control.Name, Value: toOutputValue(control, item.value) });
    }
  }
  return outputs;
}

/** The client value of a Computed field (Sum, Count, Min, Max; empty = 0 for Sum). Null if the expression is not known. */
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
    .filter((x) => x !== null && x !== undefined && String(x).trim() !== '' && !Number.isNaN(Number(x)))
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
