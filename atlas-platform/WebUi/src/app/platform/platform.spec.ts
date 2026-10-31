// COPY to the target with platform/. The spec uses only describe/it/expect/TestBed and matchers that Jasmine (Karma, the
// target runner) and Vitest (the local runner, globals on) both have: toBe, toEqual, toBeNull, toBeTruthy. No runner import.
import { FormControl, FormGroup } from '@angular/forms';
import { CPApplicationControl, CPApplicationSection } from './atlas-models';
import { TestBed } from '@angular/core/testing';
import { ATLAS_DASHBOARD_LINK, formRoute, formTypeOf } from './atlas-navigation';
import { AtlasEffectTarget, applyEffects, matches, optionData } from './form/atlas-effects';
import { buildFormGroup, buildOutputs, computeValue, containerOf, isShown, shownInvalid, toModelValue, toOutputValue } from './form/atlas-form-model';
import { atlasValidators, errorCode, errorText } from './form/atlas-validators';
import { ATLAS_VALIDATOR_CASES } from './form/atlas-validator-cases';
import { cellDay, formatDate, sumsRow } from './dashboard/atlas-dashboard-columns';
import { AtlasRendererRegistry, provideAtlasRenderers } from './form/atlas-renderer-registry';
import { AtlasCheckboxRenderer, AtlasRadioRenderer, AtlasTextRenderer, AtlasUnsupportedRenderer } from './form/renderers/atlas-renderers';
import { toSelectOptions } from './atlas-pbdev';
import { problemText } from './atlas-errors';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { provideToastr } from 'ngx-toastr';
import { AtlasApiService } from './atlas-api.service';
import { AtlasDashboardComponent } from './dashboard/atlas-dashboard.component';

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
  for (const [value, key] of [
    ['1.5', 'integer'],
    ['1:30', 'integer'],
    ['-5', 'min'],
    ['1441', 'max'],
  ]) {
    it(`refuses ${value} with ${key}`, () => {
      const control = new FormControl(value, atlasValidators(minutes, false));
      expect(control.errors?.[key]).toBeTruthy();
    });
  }

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

describe('renderer registry', () => {
  it('gives no label target for types without an input, and register can change it', () => {
    const registry = new AtlasRendererRegistry();
    expect(registry.hasInput('select')).toBe(true);
    expect(registry.hasInput('ngselect')).toBe(true);
    expect(registry.hasInput('User')).toBe(false);
    expect(registry.hasInput('grid')).toBe(false);
    registry.register('badge', AtlasTextRenderer, false);
    expect(registry.hasInput('badge')).toBe(false);
    registry.register('user', AtlasTextRenderer);
    expect(registry.hasInput('user')).toBe(true);
  });
});

describe('@pbdev adapter', () => {
  it('maps CPKeyValue to the label/value options of pbdev-select', () => {
    expect(toSelectOptions([{ Label: 'Tax', Value: 'TAX_N' }, { Value: 'OTHER' }, {}])).toEqual([
      { label: 'Tax', value: 'TAX_N' },
      { label: 'OTHER', value: 'OTHER' },
      { label: '', value: '' },
    ]);
    expect(toSelectOptions(null)).toEqual([]);
  });
});

describe('dashboard link (review NG-02)', () => {
  it('goes to the target route dashboard/:code by default', () => {
    expect(TestBed.inject(ATLAS_DASHBOARD_LINK)('TTDV2')).toEqual(['/dashboard', 'TTDV2']);
  });
});

describe('hidden fields (review NG-03)', () => {
  const form: CPApplicationSection = {
    Controls: [
      {
        Name: 'SectionA', Label: 'A', Type: 'section',
        ChildControls: [
          { Name: 'Answer', Label: 'Answer', Type: 'select', Value: 'No' },
          { Name: 'Reason', Label: 'Reason', Type: 'text', Value: '', Validator: { Required: true } },
        ],
      },
      {
        Name: 'SectionB', Label: 'B', Type: 'section',
        ChildControls: [{ Name: 'Detail', Label: 'Detail', Type: 'text', Value: 'old', Validator: { Required: true } }],
      },
    ],
  };

  it('does not stop the save for a hidden required field and does not send it', () => {
    const group = buildFormGroup(form);
    const hidden = new Set(['Reason']);
    const shown = (name: string) => isShown(name, containerOf(form), (x) => !hidden.has(x));

    expect(group.invalid).toBe(true); // Angular alone would stop the save
    expect(shownInvalid(form, group, shown)).toBe(false);
    expect(buildOutputs(form, group, shown).map((x) => x.Name)).toEqual(['Answer', 'Detail']);
  });

  it('treats the children of a hidden section as hidden', () => {
    const group = buildFormGroup(form);
    group.get('Reason')!.setValue('Because');
    group.get('Detail')!.setValue('');
    const hidden = new Set(['SectionB']);
    const shown = (name: string) => isShown(name, containerOf(form), (x) => !hidden.has(x));

    expect(shown('Detail')).toBe(false);
    expect(shownInvalid(form, group, shown)).toBe(false);
    expect(buildOutputs(form, group, shown).map((x) => x.Name)).toEqual(['Answer', 'Reason']);
  });

  it('still stops the save for a shown required field', () => {
    const group = buildFormGroup(form);
    const shown = (name: string) => isShown(name, containerOf(form), () => true);

    expect(shownInvalid(form, group, shown)).toBe(true);
  });
});

describe('control types (review NG-04)', () => {
  it('knows the types of the current JSON and gives a note for a type with no renderer', () => {
    const registry = new AtlasRendererRegistry();
    expect(registry.resolve('check')).toBe(AtlasCheckboxRenderer);
    expect(registry.resolve('option')).toBe(AtlasRadioRenderer);
    expect(registry.has('filepicker')).toBe(false);
    expect(registry.resolve('filepicker')).toBe(AtlasUnsupportedRenderer);
  });

  it('adds the renderers of an app with provideAtlasRenderers', () => {
    TestBed.configureTestingModule({ providers: [provideAtlasRenderers({ rating: AtlasTextRenderer }, ['rating'])] });
    const registry = TestBed.inject(AtlasRendererRegistry);
    expect(registry.get('rating')).toBe(AtlasTextRenderer);
    expect(registry.hasInput('rating')).toBe(false);
  });
});

describe('validator cases shared with the server (review SV-04)', () => {
  for (const item of ATLAS_VALIDATOR_CASES) {
    it(`${JSON.stringify(item.validator)} "${item.value}" gives ${item.code ?? 'no error'}`, () => {
      const control = new FormControl(item.value, atlasValidators(item.validator, !!item.validator.Required));
      expect(errorCode(control.errors)).toBe(item.code);
      expect(errorText(control.errors, 'Field', item.validator)).toBe(item.message);
    });
  }
});

describe('dashboard rows of another app (review NG-07)', () => {
  it('sends Delete and History to the app of the tab, not the dashboard app', () => {
    const removed: string[] = [];
    const api = {
      applications: () => of([]),
      dashboard: () => of({ Name: 'D', Tabs: [{ ID: 'T', Label: 'T', Code: 'OTHERAPP', Visible: true, KeyValues: [{ ID: 7 }], Columns: [] }], Buttons: [] }),
      remove: (code: string, id: number) => {
        removed.push(`${code} ${id}`);
        return of({ Errors: [] });
      },
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideToastr(),
        { provide: AtlasApiService, useValue: api },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ code: 'ttdv2' })) } },
      ],
    });
    const component = TestBed.createComponent(AtlasDashboardComponent).componentInstance;
    component.ngOnInit();

    const row = component.rows()[0];
    component.historyRow.set(component.rowRef(row));
    expect(component.historyRow()).toEqual({ code: 'OTHERAPP', id: 7 });

    component.deleteRow.set(component.rowRef(row));
    component.confirmRemove();
    expect(removed).toEqual(['OTHERAPP 7']);
    expect(component.deleteRow()).toBeNull();
  });
});

describe('problemText (review NG-12)', () => {
  it('gives the user a text for each HTTP error, with no developer hint', () => {
    expect(problemText({ status: 0 })).toBe('The service is not available. Try again later.');
    expect(problemText({ status: 403, error: { detail: 'No role.' } })).toBe('Access denied: No role.');
    expect(problemText({ status: 404 })).toBe('Not found.');
    expect(problemText({ status: 500, error: { title: 'Server error' } })).toBe('Server error');
  });
});

describe('dashboard tabs keyboard (review NG-08)', () => {
  it('moves between the tabs with the arrow keys, Home and End', () => {
    const api = {
      applications: () => of([]),
      dashboard: () => of({ Name: 'D', Tabs: ['A', 'B', 'C'].map((x) => ({ ID: x, Label: x, Visible: true, KeyValues: [], Columns: [] })), Buttons: [] }),
      remove: () => of({ Errors: [] }),
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideToastr(),
        { provide: AtlasApiService, useValue: api },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ code: 'ttdv2' })) } },
      ],
    });
    const component = TestBed.createComponent(AtlasDashboardComponent).componentInstance;
    component.ngOnInit();
    const key = (k: string) => component.onTabKey(new KeyboardEvent('keydown', { key: k }));

    key('ArrowRight');
    expect(component.tabIndex()).toBe(1);
    key('End');
    expect(component.tabIndex()).toBe(2);
    key('ArrowRight');
    expect(component.tabIndex()).toBe(0);
    key('ArrowLeft');
    expect(component.tabIndex()).toBe(2);
    key('Home');
    expect(component.tabIndex()).toBe(0);
    key('a');
    expect(component.tabIndex()).toBe(0);
  });
});

describe('dashboard as cp-atlas-dashboard (review R5)', () => {
  it('uses the target layout, search box and delete message', () => {
    const api = {
      applications: () => of([]),
      dashboard: () => of({ Name: 'Tax Time Database', Tabs: [{ ID: 'A', Label: 'All', Description: 'All records', Visible: true, KeyValues: [{ ID: 7 }], Columns: [] }], Buttons: [] }),
      remove: () => of({ Errors: [] }),
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideToastr(),
        { provide: AtlasApiService, useValue: api },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ code: 'ttdv2' })) } },
      ],
    });
    const fixture = TestBed.createComponent(AtlasDashboardComponent);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelector('.header-container h1.header-title')?.textContent?.trim()).toBe('Tax Time Database');
    expect(root.querySelector('ul.nav.nav-tabs a.nav-link.active')?.textContent?.trim()).toBe('All');
    expect(root.querySelector('.tabs-content p')?.textContent?.trim()).toBe('All records');
    const search = root.querySelector('#txt_Search') as HTMLInputElement;
    search.value = 'abc';
    search.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.search()).toBe('abc');
    fixture.componentInstance.selectTab(0);
    expect(fixture.componentInstance.search()).toBe('');

    fixture.componentInstance.deleteRow.set({ code: 'TTDV2', id: 7 });
    fixture.detectChanges();
    expect(root.querySelector('.modal-confirm-body')?.textContent?.trim()).toBe('Are you sure you want to delete the record?');
  });
});

describe('value kinds of the real pbdev controls (review R2)', () => {
  const date = { Name: 'D', Label: 'D', Type: 'datetimepicker' } as CPApplicationControl;
  const check = { Name: 'C', Label: 'C', Type: 'check' } as CPApplicationControl;
  const list = { Name: 'L', Label: 'L', Type: 'ngselect', Multiple: true } as CPApplicationControl;

  it('reads a date as a local Date and writes yyyy-MM-dd', () => {
    const value = toModelValue(date, '2026-02-28') as Date;
    expect([value.getFullYear(), value.getMonth(), value.getDate()]).toEqual([2026, 1, 28]);
    expect(toOutputValue(date, value)).toBe('2026-02-28');
    expect(toModelValue(date, '')).toBeNull();
    expect(toOutputValue(date, null)).toBeNull();
  });

  it('reads true/yes/1 as checked and writes true/false', () => {
    expect(['true', 'Yes', '1', 'false', '', null].map((x) => toModelValue(check, x))).toEqual([true, true, true, false, false, false]);
    expect([true, false, null].map((x) => toOutputValue(check, x))).toEqual(['true', 'false', 'false']);
  });

  it('splits and joins a list with #$#', () => {
    expect(toModelValue(list, 'A#$#B')).toEqual(['A', 'B']);
    expect(toModelValue(list, null)).toEqual([]);
    expect(toOutputValue(list, ['A', 'B'])).toBe('A#$#B');
    expect(toOutputValue(list, [])).toBeNull();
  });
});

describe('dashboard date filter (review 3 RV-16)', () => {
  it('filters on the same day that the cell shows', () => {
    const stamp = '2026-10-18T02:30:00.000Z';
    const shown = formatDate(stamp);
    const day = cellDay(stamp)!;
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    expect(`${String(day.getDate()).padStart(2, '0')}-${months[day.getMonth()]}-${day.getFullYear()}`).toBe(shown);
    expect(cellDay('2026-10-18')!.getDate()).toBe(18);
    expect(cellDay('')).toBeNull();
  });
});

describe('history time (review 3 RV-L19)', () => {
  it('reads the local time form with a space', async () => {
    const { localTime } = await import('./atlas-history-dialog');
    const value = localTime('2026-10-25 10:05:30');
    expect(value instanceof Date).toBe(true);
    expect((value as Date).getHours()).toBe(10);
    expect((value as Date).getSeconds()).toBe(30);
    expect(localTime('not a date')).toBe('not a date');
  });
});
