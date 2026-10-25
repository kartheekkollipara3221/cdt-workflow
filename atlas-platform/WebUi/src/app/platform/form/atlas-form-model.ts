// COPY to the target: WebUi/Source/App/platform/form/. The form model of a section: the form group, the outputs
// for the save, and the client value of a Computed field. Plain functions, so the unit tests need no component.
import { FormControl, FormGroup } from '@angular/forms';
import { CPApplicationControl, CPApplicationSection, CPPageOutput } from '../atlas-models';
import { atlasValidators } from './atlas-validators';

export const SINGLE_ROW_FIELDS = 'SingleRowFields';

export function isSection(control: CPApplicationControl): boolean {
  return (control.Type ?? '').toLowerCase() === 'section';
}

export function isSingleRowGrid(control: CPApplicationControl): boolean {
  return (control.Grid?.BindMode ?? '').toLowerCase() === SINGLE_ROW_FIELDS.toLowerCase();
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

/** One form control for each value control; a nested group for each SingleRowFields grid. */
export function buildFormGroup(section: CPApplicationSection): FormGroup {
  const group = new FormGroup<Record<string, FormControl | FormGroup>>({});
  for (const control of valueControls(section)) {
    if (isSingleRowGrid(control)) {
      const row = control.Grid?.Data?.[0] ?? {};
      const rowGroup = new FormGroup<Record<string, FormControl>>({});
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
        new FormControl<string | null>(
          { value: control.Value ?? '', disabled: !!control.Disabled },
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
      const value = item.value;
      outputs.push({ Name: control.Name, Value: value === null || value === undefined || value === '' ? null : String(value) });
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
