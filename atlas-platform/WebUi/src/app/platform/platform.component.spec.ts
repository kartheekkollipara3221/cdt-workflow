// COPY to the target with platform/. Component tests (review NG-09) with TestBed and HttpTestingController. The spec
// uses only describe/it/expect/TestBed and matchers that Jasmine (Karma, the target runner) and Vitest (the local
// runner, globals on) both have. No runner import. The last test runs with provideZoneChangeDetection, as the target
// app.config.ts; it needs zone.js (loaded by Karma at the target; not installed locally, so it returns early there).
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideZoneChangeDetection } from '@angular/core';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CPApplicationControl, CPApplicationSection } from './atlas-models';
import { ATLAS_DASHBOARD_LINK } from './atlas-navigation';
import { AtlasFormComponent } from './form/atlas-form.component';
import { AtlasRendererRegistry } from './form/atlas-renderer-registry';

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
