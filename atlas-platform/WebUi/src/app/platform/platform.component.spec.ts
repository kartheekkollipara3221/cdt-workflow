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
import { ATLAS_DASHBOARD_LINK } from './atlas-navigation';
import { AtlasFormComponent } from './form/atlas-form.component';
import { AtlasHistoryDialog } from './atlas-history-dialog';
import { AtlasRendererRegistry } from './form/atlas-renderer-registry';
import { provideAtlasGridComponents } from './form/atlas-grid-components';
import { Component } from '@angular/core';
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
  http.expectOne((r) => r.url === 'v1/SectionPage').flush({ result: page });
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
  const request = http.expectOne((r) => r.url === 'v1/ManagePage');
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
    http.expectOne((r) => r.url === 'v1/ManagePage').flush({
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
    const request = http.expectOne((r) => r.url === 'v1/RemovePage');
    expect(request.request.params.get('code')).toBe('TTDV2');
    expect(request.request.params.get('id')).toBe('5');
    request.flush({ ID: 5, Errors: [], Outputs: [{ Name: 'IsDeleted', Value: 'Yes' }] });

    expect(navigated).toEqual([['/home', 'TTDV2'], ['/home', 'TTDV2']]);
  });

  it('opens the history dialog, loads the log of the record, and closes it', () => {
    const { fixture, http } = setup(section([text('Name')]));

    click(fixture, '#btn_History');
    const request = http.expectOne((r) => r.url === 'v1/GetAuditLog');
    expect(request.request.params.get('appCode')).toBe('TTDV2');
    expect(request.request.params.get('refID')).toBe('5');
    request.flush({
      result: [{ AppCode: 'TTDV2', RefID: 5, CreatedBy: 'Dana', CreatedDate: '2026-10-25 10:00:00', Event: 'Update',
        LogMessage: [{ Name: '_Event', Value: 'Update' }, { Name: 'Name', Label: 'Name', Old: 'a', Value: 'b' }] }],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.historyOpen()).toBe(true);
    expect((fixture.nativeElement as HTMLElement).textContent?.includes('Dana')).toBe(true);

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
    http.expectNone((r) => r.url === 'v1/ManagePage');
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
    const first = http.expectOne((r) => r.url === 'v1/GetAuditLog' && r.params.get('refID') === '5');

    fixture.componentRef.setInput('recordId', 7);
    fixture.detectChanges();
    const second = http.expectOne((r) => r.url === 'v1/GetAuditLog' && r.params.get('refID') === '7');

    expect(first.cancelled).toBe(true);
    second.flush({ result: [{ AppCode: 'TTDV2', RefID: 7, CreatedBy: 'Seven', CreatedDate: '2026-10-25 10:00:00', Event: 'Create', LogMessage: [] }] });
    fixture.detectChanges();
    expect(fixture.componentInstance.entries().map((x) => x.by)).toEqual(['Seven']);
  });

  it('shows the error text, not "No history", when the load fails', () => {
    const { fixture, http } = dialog();

    http.expectOne((r) => r.url === 'v1/GetAuditLog').flush({ detail: 'No History permission.' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toBe('Access denied: No History permission.');
  });
});
