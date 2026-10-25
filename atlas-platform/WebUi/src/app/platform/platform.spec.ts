import { FormControl, FormGroup } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { CPApplicationControl, CPApplicationSection } from './atlas-models';
import { formRoute, formTypeOf } from './atlas-navigation';
import { AtlasEffectTarget, applyEffects, matches, optionData } from './form/atlas-effects';
import { buildFormGroup, buildOutputs, computeValue } from './form/atlas-form-model';
import { atlasValidators, errorText } from './form/atlas-validators';
import { formatDate, sumsRow } from './dashboard/atlas-dashboard-columns';

class Recorder implements AtlasEffectTarget {
  calls: string[] = [];
  setVisible(name: string, visible: boolean) { this.calls.push(`visible ${name} ${visible}`); }
  setRequired(name: string, required: boolean) { this.calls.push(`required ${name} ${required}`); }
  setEnabled(name: string, enabled: boolean) { this.calls.push(`enabled ${name} ${enabled}`); }
  setValue(name: string, value: string | null) { this.calls.push(`value ${name} ${value}`); }
}

const minutes = { Integer: true, Min: 0, Max: 1440 };

const section: CPApplicationSection = {
  Controls: [
    {
      Name: 'Box', Label: 'Box', Type: 'section',
      ChildControls: [
        { Name: 'Category', Label: 'Category', Type: 'select', Field: 'Category', Value: 'TAX_N', Validator: { Required: true } },
        { Name: 'ModifiedBy', Label: 'Modified', Type: 'user', Field: 'ModifiedBy', Value: 'jdoe', Disabled: true },
        { Name: 'Secret', Label: 'Secret', Type: 'text', Field: 'Secret', Value: 'x' },
      ],
    },
    {
      Name: 'WeeklyHour', Label: 'Hours', Type: 'grid', Validator: minutes,
      Grid: {
        BindMode: 'SingleRowFields',
        Columns: [{ field: 'MondayMinute' }, { field: 'TuesdayMinute' }, { field: 'WeeklyTotal', cellRendererParams: { disabled: true } }],
        Data: [{ MondayMinute: '60', TuesdayMinute: '', WeeklyTotal: '60' }],
      },
    },
  ],
};

describe('effects (spec 01, 5.1)', () => {
  it('matches the four conditions', () => {
    expect(matches({ Equals: 'No' }, 'no')).toBe(true);
    expect(matches({ NotEquals: 'No' }, 'No')).toBe(false);
    expect(matches({ In: ['A', 'B'] }, 'B')).toBe(true);
    expect(matches({ HasValue: true }, '')).toBe(false);
    expect(matches(null, 'x')).toBe(true);
  });

  it('runs the actions of the matching effects, with SetFromOption from Data', () => {
    const control: CPApplicationControl = {
      Name: 'Account', Label: 'Account', Type: 'ngselect',
      Options: [{ Label: '1 - A', Value: '1 - A', Data: { AccountNumber: '1', AccountName: 'A Trust' } }],
      Effects: [
        { When: { HasValue: true }, SetFromOption: { AccountNumber: 'AccountNumber', AccountName: 'AccountName' }, Show: ['X'] },
        { When: { HasValue: false }, Clear: ['AccountNumber'], Hide: ['X'] },
      ],
    };
    const target = new Recorder();
    applyEffects(control, '1 - A', target);
    expect(target.calls).toEqual(['visible X true', 'value AccountNumber 1', 'value AccountName A Trust']);

    const cleared = new Recorder();
    applyEffects(control, '', cleared);
    expect(cleared.calls).toEqual(['visible X false', 'value AccountNumber null']);
  });

  it('falls back to the " - " split when an option has no Data', () => {
    expect(optionData([{ Value: '12345 - Smith Trust' }], '12345 - Smith Trust')).toEqual({ AccountNumber: '12345', AccountName: 'Smith Trust' });
  });
});

describe('validators (spec 01, 10.3)', () => {
  it.each([
    ['1.5', 'integer'],
    ['1:30', 'integer'],
    ['-5', 'min'],
    ['1441', 'max'],
  ])('refuses %s with %s', (value, key) => {
    const control = new FormControl(value, atlasValidators(minutes, false));
    expect(control.errors?.[key]).toBeTruthy();
  });

  it('accepts 0, 1440 and empty', () => {
    for (const value of ['0', '1440', '']) {
      expect(new FormControl(value, atlasValidators(minutes, false)).valid).toBe(true);
    }
  });

  it('puts a server message first', () => {
    expect(errorText({ server: 'Taken.', required: true }, 'X')).toBe('Taken.');
    expect(errorText({ required: true }, 'Category')).toBe('Category is required.');
  });
});

describe('form model', () => {
  it('builds controls, a grid row group, and the outputs of visible and enabled fields only', () => {
    const group = buildFormGroup(section);
    expect(group.get('ModifiedBy')?.disabled).toBe(true);
    expect((group.get('WeeklyHour') as FormGroup).get('WeeklyTotal')?.disabled).toBe(true);

    group.get('Category')?.setValue('OTHER');
    const outputs = buildOutputs(section, group, (name) => name !== 'Secret');
    expect(outputs.map((x) => x.Name)).toEqual(['Category', 'WeeklyHour']);
    expect(JSON.parse(outputs[1].Value!)).toEqual([{ MondayMinute: '60', TuesdayMinute: '' }]);
  });

  it('computes Sum with empty = 0, Count, Min and Max', () => {
    const values = { A: '60', B: '', C: '30' };
    expect(computeValue('Sum(A, B, C)', values)).toBe(90);
    expect(computeValue('Count(A, B, C)', values)).toBe(2);
    expect(computeValue('Min(A, C)', values)).toBe(30);
    expect(computeValue('Max(A, C)', values)).toBe(60);
    expect(computeValue('A + B', values)).toBeNull();
  });
});

describe('navigation (spec 12, question 5)', () => {
  it('uses FormType first and gives null for the current if chain', () => {
    expect(formRoute('FORM', 'TTDV2', 0)).toEqual(['/form', 'TTDV2', 0]);
    expect(formRoute('SECTION', 'TTD', 4)).toEqual(['/sectionmaster', 'TTD', 4]);
    expect(formRoute(null, 'CDT', 1)).toBeNull();
    expect(formTypeOf([{ ID: 1, Name: 'x', Code: 'TTDV2', FormType: 'FORM' }], 'ttdv2')).toBe('FORM');
  });
});

describe('dashboard columns', () => {
  it('formats dates and makes the totals row', () => {
    expect(formatDate('2026-10-18')).toBe('18-Oct-2026');
    const total = sumsRow({ Columns: [{ Name: 'Account' }, { Name: 'Minutes', Sum: true }] }, [{ Minutes: 30 }, { Minutes: 15 }, { Minutes: null }]);
    expect(total).toEqual({ Account: 'Total', Minutes: 45 });
  });
});
