// COPY to the target with platform/. Component tests (review NG-09) with TestBed and HttpTestingController. The spec
// uses only describe/it/expect/TestBed and matchers that Jasmine (Karma, the target runner) and Vitest (the local
// runner, globals on) both have. No runner import. The last test runs with provideZoneChangeDetection, as the target
// app.config.ts; it needs zone.js (loaded by Karma at the target; not installed locally, so it returns early there).
import { provideToastr } from 'ngx-toastr';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideZoneChangeDetection } from '@angular/core';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CPApplicationControl, CPApplicationSection } from './atlas-models';
import { ATLAS_DASHBOARD_LINK, AtlasDashboardNavigation } from './atlas-navigation';
import { AtlasFormComponent } from './form/atlas-form.component';
import { AtlasHistoryDialog } from './atlas-history-dialog';
import { AtlasRendererRegistry } from './form/atlas-renderer-registry';
import { provideAtlasGridComponents } from './form/atlas-grid-components';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AllCommunityModule, ModuleRegistry } from 'ag-grid-community';

// As the target main.ts (it registers AllEnterpriseModule): the grids use the registered modules.
ModuleRegistry.registerModules([AllCommunityModule]);

/** A cell component as the target NumericEditor: an input with the id ctrlname_row that calls params.setValue on blur. */
@Component({
  selector: 'test-numeric-cell',
  template: `<input [id]="id" [value]="value" [disabled]="disabled" (input)="value = $any($event.target).value" (blur)="params.setValue(value)" />`,
})
class TestNumericCell {
  params!: { value: unknown; disabled?: boolean; ctrlname: string; node: { rowIndex: number }; setValue: (v: unknown) => void };
  value = '';
  id = '';
  disabled = false;
  agInit(params: TestNumericCell['params']): void {
    this.params = params;
    this.value = String(params.value ?? '');
    this.id = params.ctrlname + '_' + params.node.rowIndex;
    this.disabled = !!params.disabled;
  }
  refresh(params: TestNumericCell['params']): boolean {
    this.agInit(params);
    return true;
  }
}

/**
 * A cell component that calls params.ngModelChange for each key, as the target NumericEditor.
 * Its refresh changes the value, and the input shows it at the next change detection.
 */
@Component({
  selector: 'test-typing-cell',
  imports: [FormsModule],
  template: `<input [id]="id" [(ngModel)]="value" (ngModelChange)="onInput($event)" [disabled]="disabled" (blur)="params.setValue(value)" />`,
})
class TestTypingCell {
  params!: TestNumericCell['params'] & { ngModelChange?: (p: { value: unknown; rowData: unknown }) => void };
  value = '';
  id = '';
  disabled = false;
  agInit(params: TestTypingCell['params']): void {
    this.params = params;
    this.value = String(params.value ?? '');
    this.id = params.ctrlname + '_' + params.node.rowIndex;
    this.disabled = !!params.disabled;
  }
  onInput(value: string): void {
    this.value = value;
    this.params.ngModelChange?.({ value, rowData: null });
  }
  refresh(params: TestTypingCell['params']): boolean {
    this.agInit(params);
    return true;
  }
}

declare const Zone: unknown;

interface Setup {
  fixture: ComponentFixture<AtlasFormComponent>;
  http: HttpTestingController;
  navigated: unknown[][];
}

function text(name: string, extra: Partial<CPApplicationControl> = {}): CPApplicationControl {
  return { Name: name, Label: name, Type: 'text', Field: name, Visible: true, ...extra };
}

function section(children: CPApplicationControl[], extra: Partial<CPApplicationSection> = {}): CPApplicationSection {
  return {
    HeaderName: 'Test form',
    AppCode: 'TTDV2',
    Version: 'AAAA',
    UserPermissions: { CanCreate: true, CanEdit: true, CanDelete: true, CanViewHistory: true },
    Controls: [{ Name: 'Main', Label: 'Main', Type: 'section', Visible: true, ChildControls: children }],
    Buttons: [
      { Name: 'Submit', Label: 'Save', Visible: true, Action: { Type: 'Save' } },
      { Name: 'Close', Label: 'Close', Visible: true, Action: { Type: 'Close' } },
      { Name: 'Delete', Label: 'Delete', Visible: true, Action: { Type: 'Delete' } },
    ],
    ...extra,
  };
}

function setup(page: CPApplicationSection, id = 5, providers: unknown[] = []): Setup {
  TestBed.configureTestingModule({
    providers: [
      ...(providers as never[]),
      provideHttpClient(),
      provideToastr(),
      provideHttpClientTesting(),
      provideRouter([]),
      { provide: ATLAS_DASHBOARD_LINK, useValue: (code: string) => ['/home', code] },
      { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ code: 'ttdv2', id: String(id) })) } },
    ],
  });
  const navigated: unknown[][] = [];
  const router = TestBed.inject(Router);
  router.navigate = (commands: unknown[]) => {
    navigated.push(commands);
    return Promise.resolve(true);
  };
  const http = TestBed.inject(HttpTestingController);
  const fixture = TestBed.createComponent(AtlasFormComponent);
  fixture.detectChanges();
  http.expectOne((r) => r.url === 'SectionPage').flush({ result: page });
  fixture.detectChanges();
  return { fixture, http, navigated };
}

function element(fixture: ComponentFixture<unknown>, selector: string): HTMLElement | null {
  return (fixture.nativeElement as HTMLElement).querySelector(selector);
}

function click(fixture: ComponentFixture<unknown>, selector: string): void {
  const target = element(fixture, selector);
  expect(target === null).toBe(false);
  target!.click();
  fixture.detectChanges();
}

function savedOutputs(http: HttpTestingController): { Name: string; Value: string | null }[] {
  const request = http.expectOne((r) => r.url === 'ManagePage');
  const body = request.request.body as FormData;
  const page = JSON.parse(String(body.get('input'))) as { Outputs: { Name: string; Value: string | null }[] };
  request.flush({ ID: 5, Errors: [] });
  return page.Outputs;
}

describe('AtlasFormComponent (review NG-09)', () => {
  it('renders each registered control type with no "unsupported" note', () => {
    const types = new AtlasRendererRegistry().types();
    const children = types.map((type) =>
      text(`F_${type}`, {
        Type: type,
        Options: [{ Label: 'A', Value: 'A' }],
        Grid: type === 'grid' ? { BindMode: 'SingleRowFields', Columns: [{ field: 'Mon', headerName: 'Mon' }] } : null,
      } as Partial<CPApplicationControl>),
    );

    const { fixture } = setup(section(children));

    for (const type of types) {
      expect(element(fixture, `#div_F_${type}`) === null).toBe(false);
    }
    expect(element(fixture, '.atlas-unsupported') === null).toBe(true);
  });

  it('saves when a required field is hidden, and does not send the hidden field', () => {
    const { fixture, http, navigated } = setup(section([text('Name'), text('Secret', { Visible: false, Validator: { Required: true } })]));

    click(fixture, '#btn_Submit');

    expect(savedOutputs(http).map((x) => x.Name)).toEqual(['Name']);
    expect(navigated).toEqual([['/home', 'TTDV2']]);
  });

  it('shows a server field error on its field and other errors at the top', () => {
    const { fixture, http } = setup(section([text('Name')]));

    click(fixture, '#btn_Submit');
    http.expectOne((r) => r.url === 'ManagePage').flush({
      ID: 0,
      Errors: [
        { Field: 'Name', Code: 'Pattern', Message: 'Name has a format that is not valid.' },
        { Field: '', Code: 'Conflict', Message: 'Another user changed this record.' },
      ],
    });
    fixture.detectChanges();

    expect(element(fixture, '#div_Name .atlas-error')?.textContent?.trim()).toBe('Name has a format that is not valid.');
    expect(element(fixture, '[role=alert]')?.textContent?.includes('Another user changed this record.')).toBe(true);
  });

  it('goes to the injected dashboard link on Close and after Delete', () => {
    const { fixture, http, navigated } = setup(section([text('Name')]));

    click(fixture, '#btn_Close');
    click(fixture, '#btn_Delete');
    click(fixture, '#btn_ConfirmDelete');
    const request = http.expectOne((r) => r.url === 'RemovePage');
    expect(request.request.params.get('code')).toBe('TTDV2');
    expect(request.request.params.get('id')).toBe('5');
    request.flush({ ID: 5, Errors: [], Outputs: [{ Name: 'IsDeleted', Value: 'Yes' }] });

    expect(navigated).toEqual([['/home', 'TTDV2'], ['/home', 'TTDV2']]);
  });

  it('opens the history dialog, loads the log of the record, and closes it', () => {
    const { fixture, http } = setup(section([text('Name')]));

    click(fixture, '#btn_History');
    const request = http.expectOne((r) => r.url === 'GetAuditLog');
    expect(request.request.params.get('appCode')).toBe('TTDV2');
    expect(request.request.params.get('refID')).toBe('5');
    request.flush({
      result: [{ AppCode: 'TTDV2', RefID: 5, CreatedBy: 'Dana', CreatedDate: '10-25-2026 10:00:00', Event: 'Update',
        LogMessage: '[{"Name":"Name","Label":"Name","Old":"a","Value":"b"}]' }],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.historyOpen()).toBe(true);
    const shown = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(shown.includes('Modified On : 10-25-2026 10:00:00')).toBe(true);
    expect(shown.includes('Modified By : Dana')).toBe(true);
    expect(shown.includes('Name - b')).toBe(true);

    fixture.componentInstance.historyOpen.set(false);
    fixture.detectChanges();
    expect(fixture.componentInstance.historyOpen()).toBe(false);
    http.verify();
  });

  it('links the error, the required state and the label to the input (review NG-08)', () => {
    const { fixture, http } = setup(section([text('Name', { Validator: { Required: true } }), text('Done', { Type: 'checkbox' })]));

    click(fixture, '#btn_Submit');
    fixture.detectChanges();

    const input = element(fixture, '#ctl_Name')!;
    expect(input.getAttribute('aria-required')).toBe('true');
    expect(input.getAttribute('aria-describedby')).toBe('err_Name');
    expect(element(fixture, '#err_Name')?.getAttribute('aria-live')).toBe('polite');
    expect(element(fixture, '#div_Done label')?.getAttribute('for')).toBeNull();
    expect(element(fixture, '#div_Done input')?.getAttribute('aria-labelledby')).toBe('lbl_Done');
    http.verify();
  });

  it('saves in the change detection mode of the target (provideZoneChangeDetection)', () => {
    if (typeof Zone === 'undefined') {
      expect(true).toBe(true); // zone.js is not loaded (local Vitest); the target Karma runs this test
      return;
    }
    const { fixture, http, navigated } = setup(section([text('Name')]), 5, [provideZoneChangeDetection({ eventCoalescing: true })]);

    click(fixture, '#btn_Submit');

    expect(savedOutputs(http).map((x) => x.Name)).toEqual(['Name']);
    expect(navigated).toEqual([['/home', 'TTDV2']]);
  });
});

describe('AtlasFormComponent with the real pbdev value behaviour (review R2)', () => {
  it('gives the date picker a Date and sends yyyy-MM-dd', () => {
    const { fixture, http } = setup(section([text('WeekEnding', { Type: 'datetimepicker', Value: '2026-10-25' })]));

    // The text format of the input belongs to the library (PrimeNG at the target shows 10/25/2026): check the value.
    expect(element(fixture, '#ctl_WeekEnding') === null).toBe(false);
    const control = fixture.componentInstance.form.get('WeekEnding');
    expect(control?.value instanceof Date && control.value.getFullYear() * 10000 + (control.value.getMonth() + 1) * 100 + control.value.getDate()).toBe(20261025);
    control?.setValue(new Date(2026, 10, 1));
    fixture.detectChanges();

    click(fixture, '#btn_Submit');
    expect(savedOutputs(http).find((x) => x.Name === 'WeekEnding')?.Value).toBe('2026-11-01');
  });

  it('shows a stored "false" as not checked and sends true/false', () => {
    const { fixture, http } = setup(section([text('Done', { Type: 'check', Value: 'false' })]));

    const box = element(fixture, '#div_Done input[type=checkbox]') as HTMLInputElement;
    expect(box.checked).toBe(false);
    box.click();
    fixture.detectChanges();

    click(fixture, '#btn_Submit');
    expect(savedOutputs(http).find((x) => x.Name === 'Done')?.Value).toBe('true');
  });

  it('joins a multiple ng-select value with #$# as the section master', () => {
    const { fixture, http } = setup(section([text('Tags', { Type: 'ngselect', Multiple: true, Value: 'A#$#B', Options: [{ Label: 'A', Value: 'A' }, { Label: 'B', Value: 'B' }] })]));

    expect(fixture.componentInstance.form.get('Tags')?.value).toEqual(['A', 'B']);
    click(fixture, '#btn_Submit');
    expect(savedOutputs(http).find((x) => x.Name === 'Tags')?.Value).toBe('A#$#B');
  });

  it('shows the error text once, in the pbdev control, with the platform text', () => {
    const { fixture } = setup(section([text('Name', { Validator: { Required: true } })]));

    click(fixture, '#btn_Submit');

    const shown = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('#div_Name pbdev-control-errors li')).map((x) => x.textContent?.trim());
    expect(shown).toEqual(['Name is required.']);
    expect(element(fixture, '#err_Name')?.classList.contains('atlas-sr-only')).toBe(true);
  });

  it('shows the mandatory-field modal of the section master and does not save', () => {
    const { fixture, http } = setup(section([text('Name', { Validator: { Required: true } })]));

    click(fixture, '#btn_Submit');

    expect(element(fixture, '#msg_Invalid')?.textContent?.trim()).toBe('Please make sure all the mandatory fields are filled.');
    http.expectNone((r) => r.url === 'ManagePage');
    click(fixture, '#btn_InvalidOk');
    expect(element(fixture, '#msg_Invalid')).toBeNull();
  });

  it('uses the page layout and button classes of the section master', () => {
    const { fixture } = setup(section([text('Name')], { Name: 'Tax Time Database', Description: 'Weekly hours' }));

    expect(element(fixture, '.center-container h1')?.textContent?.trim()).toBe('Tax Time Database');
    expect(element(fixture, '.header-container h1.header-title.headline-3')?.textContent?.trim()).toBe('Test form');
    expect(element(fixture, '#div_Main .section.section-blue') === null).toBe(false);
    // The real library adds Angular classes (ng-star-inserted) when animations are on: check our classes only.
    expect(['header-btn', 'btn', 'btn--primary'].every((x) => element(fixture, '#btn_Close')?.classList.contains(x))).toBe(true);
    expect(element(fixture, '#div_Name label .label-text')?.textContent?.trim()).toBe('Name');
  });

  it('clears a currency value of only the currency sign', () => {
    const { fixture } = setup(section([text('Amount', { Type: 'currency' })]));

    const input = element(fixture, '#ctl_Amount') as HTMLInputElement;
    input.value = '$ ';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.form.get('Amount')?.value).toBe('');
  });
});

describe('AtlasFormComponent grid with ag-grid and the cell components (review R4)', () => {
  const day = (field: string, extra: Record<string, unknown> = {}) => ({
    headerName: field, field, cellRenderer: 'NumericEditor', cellRendererParams: { disabled: false, code: 'TTDV2', ctrlname: field, ...extra },
  });
  const grid = () =>
    ({
      Name: 'WeeklyHour', Label: 'Weekly Hours', Type: 'grid', Visible: true,
      Validator: { Integer: true, Min: 0, Max: 1440, Message: 'Enter whole minutes from 0 to 1440.' },
      Grid: { BindMode: 'SingleRowFields', Columns: [day('Mon'), day('Tue'), day('Total', { disabled: true })], Data: [{ Mon: '60', Tue: '', Total: '60' }] },
    }) as CPApplicationControl;
  const page = () => ({ ...section([text('Name')]), Computed: { Total: 'Sum(Mon, Tue)' }, Controls: [...section([text('Name')]).Controls!, grid()] });

  async function ready(fixture: ComponentFixture<AtlasFormComponent>): Promise<void> {
    for (let i = 0; i < 5 && !element(fixture, '#Mon_0'); i++) {
      await new Promise((resolve) => setTimeout(resolve, 20));
      fixture.detectChanges();
    }
  }

  /** ag-grid-angular sends grid events after the current task. */
  async function type(fixture: ComponentFixture<AtlasFormComponent>, id: string, value: string): Promise<void> {
    const input = element(fixture, id) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    for (let i = 0; i < 3; i++) {
      await new Promise((resolve) => setTimeout(resolve, 10));
      fixture.detectChanges();
    }
  }

  it('renders the row with the named cell component, in the ag-theme-pbdev grid', async () => {
    const { fixture } = setup(page(), 5, [provideAtlasGridComponents({ NumericEditor: TestNumericCell })]);
    await ready(fixture);

    const host = element(fixture, '#div_WeeklyHour ag-grid-angular');
    expect(host === null).toBe(false);
    expect((element(fixture, '#Mon_0') as HTMLInputElement).value).toBe('60');
    expect((element(fixture, '#Total_0') as HTMLInputElement).disabled).toBe(true);
  });

  it('puts a cell value in the form, shows the Computed total, and saves one JSON row', async () => {
    const { fixture, http } = setup(page(), 5, [provideAtlasGridComponents({ NumericEditor: TestNumericCell })]);
    await ready(fixture);

    await type(fixture, '#Tue_0', '30');

    expect(fixture.componentInstance.form.get('WeeklyHour.Tue')?.value).toBe('30');
    expect((element(fixture, '#Total_0') as HTMLInputElement).value).toBe('90');
    click(fixture, '#btn_Submit');
    expect(JSON.parse(savedOutputs(http).find((x) => x.Name === 'WeeklyHour')!.Value!)).toEqual([{ Mon: '60', Tue: '30' }]);
  });

  it('shows a cell error under the grid', async () => {
    const { fixture } = setup(page(), 5, [provideAtlasGridComponents({ NumericEditor: TestNumericCell })]);
    await ready(fixture);

    await type(fixture, '#Mon_0', '2000');

    expect(element(fixture, '#div_WeeklyHour .atlas-error')?.textContent?.trim()).toBe('Mon: Enter whole minutes from 0 to 1440.');
  });

  it('shows the day headers of the WeekEnding date and changes them with the date (HeaderFromDate, as the target)', async () => {
    const dated = grid();
    dated.Grid!.Columns![0].HeaderFromDate = { Control: 'WeekEnding', AddDays: -6, Format: 'EEEE dd-MMM-yyyy' };
    dated.Grid!.Columns![1].HeaderFromDate = { Control: 'WeekEnding', AddDays: 0 };
    const controls = [text('Name'), text('WeekEnding', { Value: '2026-10-25' })];
    const { fixture } = setup({ ...section(controls), Controls: [...section(controls).Controls!, dated] }, 5, [
      provideAtlasGridComponents({ NumericEditor: TestNumericCell }),
    ]);
    await ready(fixture);
    const headers = () =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('#div_WeeklyHour .ag-header-cell-text')).map((x) => x.textContent?.trim());

    expect(headers()).toEqual(['Monday 19-Oct-2026', 'Sunday 25-Oct-2026', 'Total']);

    fixture.componentInstance.form.get('WeekEnding')!.setValue('2026-11-01');
    for (let i = 0; i < 3; i++) {
      await new Promise((resolve) => setTimeout(resolve, 20));
      fixture.detectChanges();
    }
    expect(headers()).toEqual(['Monday 26-Oct-2026', 'Sunday 01-Nov-2026', 'Total']);

    fixture.componentInstance.form.get('WeekEnding')!.setValue('');
    for (let i = 0; i < 3; i++) {
      await new Promise((resolve) => setTimeout(resolve, 20));
      fixture.detectChanges();
    }
    expect(headers()).toEqual(['Mon', 'Tue', 'Total']);
  });

  it('clears the cells when the user changes the week, but not on load or for the same week (On "Change")', async () => {
    const week = text('WeekEnding', { Value: '2026-10-25', Effects: [{ On: 'Change', Clear: ['Mon', 'Tue'] }] });
    const controls = [text('Name'), week];
    const { fixture } = setup({ ...page(), Controls: [...section(controls).Controls!, grid()] }, 5, [
      provideAtlasGridComponents({ NumericEditor: TestNumericCell }),
    ]);
    await ready(fixture);
    const settle = async () => {
      for (let i = 0; i < 3; i++) {
        await new Promise((resolve) => setTimeout(resolve, 20));
        fixture.detectChanges();
      }
    };
    const form = fixture.componentInstance.form;

    expect(form.get('WeeklyHour.Mon')?.value).toBe('60');

    form.get('WeekEnding')!.setValue('2026-10-25');
    await settle();
    expect(form.get('WeeklyHour.Mon')?.value).toBe('60');

    form.get('WeekEnding')!.setValue('2026-11-01');
    await settle();
    expect(form.get('WeeklyHour.Mon')?.value).toBe('');
    expect((element(fixture, '#Mon_0') as HTMLInputElement).value).toBe('');
    expect((element(fixture, '#Total_0') as HTMLInputElement).value).toBe('0');
  });

  it('changes the total while the user types, and keeps the input that the user types in', async () => {
    const { fixture } = setup(page(), 5, [provideAtlasGridComponents({ NumericEditor: TestTypingCell })]);
    await ready(fixture);
    const input = element(fixture, '#Tue_0') as HTMLInputElement;

    input.value = '30';
    input.dispatchEvent(new Event('input'));
    for (let i = 0; i < 3; i++) {
      await new Promise((resolve) => setTimeout(resolve, 10));
      fixture.detectChanges();
    }

    expect(fixture.componentInstance.form.get('WeeklyHour.Tue')?.value).toBe('30');
    expect((element(fixture, '#Total_0') as HTMLInputElement).value).toBe('90');
    expect(element(fixture, '#Tue_0') === input).toBe(true);
  });

  it('changes the total while the user types, in the change detection mode of the target (provideZoneChangeDetection)', async () => {
    if (typeof Zone === 'undefined') {
      expect(true).toBe(true); // zone.js is not loaded (local Vitest); the target Karma runs this test
      return;
    }
    const { fixture } = setup(page(), 5, [
      provideZoneChangeDetection({ eventCoalescing: true }),
      provideAtlasGridComponents({ NumericEditor: TestTypingCell }),
    ]);
    await ready(fixture);
    const input = element(fixture, '#Tue_0') as HTMLInputElement;

    input.value = '30';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect((element(fixture, '#Total_0') as HTMLInputElement).value).toBe('90');
  });

  it('draws the total also when a host cell component wrote it in the row data first', async () => {
    const { fixture } = setup(page(), 5, [provideAtlasGridComponents({ NumericEditor: TestTypingCell })]);
    await ready(fixture);
    const agGrid = fixture.debugElement.query((x) => x.name === 'ag-grid-angular').componentInstance as {
      api: { getDisplayedRowAtIndex(i: number): { data: Record<string, unknown> } };
    };
    // As a host component that calculates a total itself: the row data changes, and the cell is not refreshed.
    agGrid.api.getDisplayedRowAtIndex(0).data['Total'] = '90';
    const input = element(fixture, '#Tue_0') as HTMLInputElement;

    input.value = '30';
    input.dispatchEvent(new Event('input'));
    for (let i = 0; i < 3; i++) {
      await new Promise((resolve) => setTimeout(resolve, 10));
      fixture.detectChanges();
    }

    expect((element(fixture, '#Total_0') as HTMLInputElement).value).toBe('90');
  });

  it('shows the cleared cells in a cell component with ngModel', async () => {
    const week = text('WeekEnding', { Value: '2026-10-25', Effects: [{ On: 'Change', Clear: ['Mon', 'Tue'] }] });
    const controls = [text('Name'), week];
    const { fixture } = setup({ ...page(), Controls: [...section(controls).Controls!, grid()] }, 5, [
      provideAtlasGridComponents({ NumericEditor: TestTypingCell }),
    ]);
    await ready(fixture);

    fixture.componentInstance.form.get('WeekEnding')!.setValue('2026-11-01');
    for (let i = 0; i < 3; i++) {
      await new Promise((resolve) => setTimeout(resolve, 20));
      fixture.detectChanges();
    }

    expect((element(fixture, '#Mon_0') as HTMLInputElement).value).toBe('');
    expect((element(fixture, '#Total_0') as HTMLInputElement).value).toBe('0');
  });

  it('lets the browser move the focus with Tab, so ag-grid does not keep it in the cell', async () => {
    const { fixture } = setup(page(), 5, [provideAtlasGridComponents({ NumericEditor: TestNumericCell })]);
    await ready(fixture);
    const grid = fixture.debugElement.query((x) => x.name === 'ag-grid-angular').componentInstance as {
      defaultColDef: { suppressKeyboardEvent: (p: { event: KeyboardEvent }) => boolean };
    };

    expect(grid.defaultColDef.suppressKeyboardEvent({ event: new KeyboardEvent('keydown', { key: 'Tab' }) })).toBe(true);
    expect(grid.defaultColDef.suppressKeyboardEvent({ event: new KeyboardEvent('keydown', { key: 'Enter' }) })).toBe(false);
  });
});

describe('AtlasHistoryDialog (review NG-10)', () => {
  function dialog() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(AtlasHistoryDialog);
    fixture.componentRef.setInput('appCode', 'TTDV2');
    fixture.componentRef.setInput('recordId', 5);
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    return { fixture, http: TestBed.inject(HttpTestingController) };
  }

  it('cancels the load of the record before, so a late answer is not shown', () => {
    const { fixture, http } = dialog();
    const first = http.expectOne((r) => r.url === 'GetAuditLog' && r.params.get('refID') === '5');

    fixture.componentRef.setInput('recordId', 7);
    fixture.detectChanges();
    const second = http.expectOne((r) => r.url === 'GetAuditLog' && r.params.get('refID') === '7');

    expect(first.cancelled).toBe(true);
    second.flush({ result: [{ AppCode: 'TTDV2', RefID: 7, CreatedBy: 'Seven', CreatedDate: '10-25-2026 10:00:00', Event: 'Create', LogMessage: '[]' }] });
    fixture.detectChanges();
    expect(fixture.componentInstance.entries().map((x) => x.by)).toEqual(['Seven']);
  });

  it('shows the error text, not "No history", when the load fails', () => {
    const { fixture, http } = dialog();

    http.expectOne((r) => r.url === 'GetAuditLog').flush({ detail: 'No History permission.' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toBe('Access denied: No History permission.');
  });
});

describe('review 3', () => {
  it('sends the Version of the load back with the save, on a URL with no v1/ prefix (RV-01, RV-18)', () => {
    const { fixture, http } = setup(section([text('Name')]));

    click(fixture, '#btn_Submit');
    const request = http.expectOne((r) => r.url === 'ManagePage');
    const page = JSON.parse(String((request.request.body as FormData).get('input'))) as { Version: string | null; ID: number };
    request.flush({ ID: 5, Errors: [] });

    expect(page.Version).toBe('AAAA');
    expect(page.ID).toBe(5);
    http.verify();
  });

  it('has no Save button and read-only fields when the server would refuse the save (RV-17)', () => {
    const { fixture } = setup(section([text('Name')], { UserPermissions: { CanCreate: true, CanEdit: false, CanDelete: false, CanViewHistory: true } }));

    expect(element(fixture, '#btn_Submit')).toBeNull();
    expect(fixture.componentInstance.form.get('Name')?.disabled).toBe(true);
  });

  it('cancels the load when the form is closed before the answer (RV-14)', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideToastr(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ code: 'ttdv2', id: '5' })) } },
      ],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(AtlasFormComponent);
    fixture.detectChanges();
    const request = http.expectOne((r) => r.url === 'SectionPage');

    fixture.destroy();

    expect(request.cancelled).toBe(true);
  });

  it('stops an effect cycle after one pass (RV-15)', () => {
    const a = text('A', { Effects: [{ When: { HasValue: true }, Set: { B: 'from A' } }] } as Partial<CPApplicationControl>);
    const b = text('B', { Effects: [{ When: { HasValue: true }, Set: { A: 'from B' } }] } as Partial<CPApplicationControl>);
    const { fixture } = setup(section([a, b]));

    fixture.componentInstance.form.get('A')!.setValue('typed');

    expect(fixture.componentInstance.form.get('B')!.value).toBe('from A');
    expect(fixture.componentInstance.form.get('A')!.value).toBe('from B');
  });

  it('opens the platform form from the target dashboard and leaves an app with no FormType to the current chain (RV-11)', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const navigated: unknown[][] = [];
    TestBed.inject(Router).navigate = (commands: unknown[]) => {
      navigated.push(commands);
      return Promise.resolve(true);
    };
    const http = TestBed.inject(HttpTestingController);
    const navigation = TestBed.inject(AtlasDashboardNavigation);

    const first = navigation.open('TTDV2', 7);
    http.expectOne((r) => r.url === 'Application').flush({ result: [{ Code: 'TTDV2', FormType: 'FORM' }, { Code: 'CDT' }] });
    expect(await first).toBe(true);
    expect(await navigation.open('CDT', 3)).toBe(false); // the rows are read once
    expect(await navigation.open('TTD', 9, { AppCode: 'TTDV2', Id: 0 })).toBe(true); // a button Target wins

    expect(navigated).toEqual([['/form', 'TTDV2', 7], ['/form', 'TTDV2', 0]]);
    http.verify();
  });
});
