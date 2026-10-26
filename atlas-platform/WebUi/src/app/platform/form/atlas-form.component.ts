// COPY to the target: WebUi/Source/App/platform/form/. The generic platform form (spec 01, 5.5; spec 12,
// question 5), route form/:code/:id, beside the current sectionmaster component. It knows no app: the controls come
// from the section JSON, the renderers from the registry, and the rules from the server.
import { NgComponentOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, FormRecord } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DialogComponent, LoaderComponent, SectionComponent } from '../atlas-pbdev';
import { AtlasApiService } from '../atlas-api.service';
import { AtlasHistoryDialog } from '../atlas-history-dialog';
import { CPApplicationControl, CPApplicationSection, CPButton, CPPageError } from '../atlas-models';
import { ATLAS_DASHBOARD_LINK } from '../atlas-navigation';
import { AtlasNoticeService } from '../atlas-notice.service';
import { problemText } from '../atlas-errors';
import { AtlasEffectTarget, applyEffects } from './atlas-effects';
import { AtlasFormRecord, buildFormGroup, buildOutputs, computeValue, containerOf, isSection, isShown, shownInvalid, valueControls } from './atlas-form-model';
import { AtlasFieldA11yDirective } from './atlas-field-a11y.directive';
import { AtlasRendererRegistry } from './atlas-renderer-registry';
import { atlasValidators, errorText } from './atlas-validators';

@Component({
  selector: 'atlas-form',
  imports: [ReactiveFormsModule, NgComponentOutlet, RouterLink, LoaderComponent, SectionComponent, DialogComponent, AtlasHistoryDialog, AtlasFieldA11yDirective],
  templateUrl: './atlas-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush, // review NG-11: the state is in signals
})
export class AtlasFormComponent implements OnInit, AtlasEffectTarget {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly dashboardLink = inject(ATLAS_DASHBOARD_LINK);
  private readonly api = inject(AtlasApiService);
  private readonly notice = inject(AtlasNoticeService);
  private readonly destroyRef = inject(DestroyRef);
  readonly registry = inject(AtlasRendererRegistry);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly pageErrors = signal<CPPageError[]>([]);
  readonly section = signal<CPApplicationSection | null>(null);
  readonly visible = signal<Record<string, boolean>>({});
  readonly required = signal<Record<string, boolean>>({});
  readonly historyOpen = signal(false);
  readonly confirmDelete = signal(false);

  // Review NG-11: route state as signals, so OnPush and zoneless see each change.
  readonly code = signal('');
  readonly id = signal(0);
  form: AtlasFormRecord = new FormRecord<FormControl | FormRecord<FormControl>>({});
  private controlsByName = new Map<string, CPApplicationControl>();
  private containers = new Map<string, string>();

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.code.set((params.get('code') ?? '').toUpperCase());
      this.id.set(Number(params.get('id') ?? 0) || 0);
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.pageErrors.set([]);
    this.api.section(this.code(), this.id()).subscribe({
      next: (section) => this.build(section),
      error: (error) => {
        this.loadError.set(problemText(error));
        this.loading.set(false);
      },
    });
  }

  private build(section: CPApplicationSection): void {
    this.controlsByName = new Map(valueControls(section).map((x) => [x.Name, x]));
    this.containers = containerOf(section);
    const visible: Record<string, boolean> = {};
    const required: Record<string, boolean> = {};
    for (const control of section.Controls ?? []) {
      visible[control.Name] = control.Visible !== false;
      for (const child of control.ChildControls ?? []) {
        visible[child.Name] = child.Visible !== false;
        required[child.Name] = !!child.Validator?.Required;
      }
    }
    this.visible.set(visible);
    this.required.set(required);
    this.form = buildFormGroup(section);
    // A control with no renderer cannot be edited, so it is not validated or sent (review NG-04).
    for (const control of this.controlsByName.values()) {
      if (!this.registry.has(control.Type)) {
        this.form.get(control.Name)?.disable({ emitEvent: false });
      }
    }
    this.section.set(section);

    // Effects run when the form is built and each time the source value changes (spec 01, 5.1).
    for (const control of this.controlsByName.values()) {
      if (!control.Effects?.length) {
        continue;
      }
      const item = this.form.get(control.Name);
      item?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value) => applyEffects(control, value, this));
    }
    for (const control of this.controlsByName.values()) {
      if (control.Effects?.length) {
        applyEffects(control, this.form.get(control.Name)?.value, this);
      }
    }

    // Computed fields: the client shows the value while the user types; the server saves its own value.
    if (section.Computed) {
      this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.updateComputed());
      this.updateComputed();
    }
    this.loading.set(false);
  }

  private updateComputed(): void {
    const section = this.section();
    if (!section?.Computed) {
      return;
    }
    const values = flatten(this.form.getRawValue() as Record<string, unknown>);
    for (const [target, expression] of Object.entries(section.Computed)) {
      const result = computeValue(expression, values);
      const control = findControl(this.form, target);
      if (control && result !== null && String(control.value ?? '') !== String(result)) {
        control.setValue(String(result), { emitEvent: false });
      }
    }
  }

  // ---------- AtlasEffectTarget ----------

  setVisible(name: string, visible: boolean): void {
    this.visible.update((x) => ({ ...x, [name]: visible }));
  }

  setRequired(name: string, required: boolean): void {
    const item = this.form.get(name);
    const control = this.controlsByName.get(name);
    if (item && control) {
      item.setValidators(atlasValidators(control.Validator, required));
      item.updateValueAndValidity({ emitEvent: false });
    }
    this.required.update((x) => ({ ...x, [name]: required }));
  }

  setEnabled(name: string, enabled: boolean): void {
    const item = this.form.get(name);
    if (enabled) {
      item?.enable({ emitEvent: false });
    } else {
      item?.disable({ emitEvent: false });
    }
  }

  setValue(name: string, value: string | null): void {
    this.form.get(name)?.setValue(value ?? '');
  }

  // ---------- Template helpers ----------

  isSection = isSection;

  isVisible(name: string): boolean {
    return this.visible()[name] !== false;
  }

  /** The control and its section are visible (review NG-03). */
  isShown(name: string): boolean {
    return isShown(name, this.containers, (x) => this.isVisible(x));
  }

  isRequired(name: string): boolean {
    return !!this.required()[name];
  }

  error(control: CPApplicationControl): string | null {
    const item = this.form.get(control.Name);
    if (!item || item instanceof FormGroup || !(item.touched || item.errors?.['server'])) {
      return null;
    }
    return errorText(item.errors, control.Label, control.Validator);
  }

  readonly visibleButtons = computed<CPButton[]>(() =>
    (this.section()?.Buttons ?? []).filter((x) => {
      if (!x.Visible) {
        return false;
      }
      if (actionOf(x) === 'delete') {
        return this.id() > 0 && !!this.section()?.UserPermissions?.CanDelete;
      }
      return true;
    }),
  );

  readonly canViewHistory = computed(() => this.id() > 0 && !!this.section()?.UserPermissions?.CanViewHistory);

  // ---------- Actions ----------

  onButton(button: CPButton): void {
    switch (actionOf(button)) {
      case 'save':
        this.save();
        break;
      case 'delete':
        this.confirmDelete.set(true);
        break;
      default:
        this.close();
    }
  }

  close(): void {
    this.router.navigate(this.dashboardLink(this.code()));
  }

  save(): void {
    const section = this.section();
    if (!section || this.saving()) {
      return;
    }
    this.form.markAllAsTouched();
    this.pageErrors.set([]);
    if (shownInvalid(section, this.form, (name) => this.isShown(name))) {
      this.pageErrors.set([{ Field: '', Code: 'Invalid', Message: 'Correct the marked fields.' }]);
      return;
    }

    this.saving.set(true);
    this.api
      .save({
        AppCode: this.code(),
        ID: this.id(),
        Version: section.Version ?? null,
        Outputs: buildOutputs(section, this.form, (name) => this.isShown(name)),
      })
      .subscribe({
        next: (result) => {
          this.saving.set(false);
          if (result.Errors?.length) {
            this.showErrors(result.Errors);
            return;
          }
          this.notice.show('success', `Record ${result.ID} saved.`);
          this.router.navigate(this.dashboardLink(this.code()));
        },
        error: (error) => {
          this.saving.set(false);
          this.pageErrors.set([{ Field: '', Code: 'Http', Message: problemText(error) }]);
        },
      });
  }

  remove(): void {
    this.confirmDelete.set(false);
    this.api.remove(this.code(), this.id()).subscribe({
      next: (result) => {
        if (result.Errors?.length) {
          this.pageErrors.set(result.Errors);
          return;
        }
        this.notice.show('success', `Record ${this.id()} deleted.`);
        this.router.navigate(this.dashboardLink(this.code()));
      },
      error: (error) => this.pageErrors.set([{ Field: '', Code: 'Http', Message: problemText(error) }]),
    });
  }

  /** Field errors go to the field; the others are listed at the top (spec 01, 5.11). */
  private showErrors(errors: CPPageError[]): void {
    const page: CPPageError[] = [];
    for (const error of errors) {
      const control = error.Field ? findControl(this.form, error.Field) : null;
      if (control && control.enabled) {
        control.setErrors({ ...(control.errors ?? {}), server: error.Message });
        control.markAsTouched();
      } else {
        page.push(error);
      }
    }
    this.pageErrors.set(page.length ? page : [{ Field: '', Code: 'Invalid', Message: 'Correct the marked fields.' }]);
  }
}

function actionOf(button: CPButton): string {
  const type = button.Action?.Type ?? (button.Name === 'Submit' ? 'Save' : button.Name);
  return (type ?? '').toLowerCase();
}

/** A control by name in the form or in a grid row group. */
function findControl(form: FormGroup, name: string): FormControl | null {
  const direct = form.get(name);
  if (direct instanceof FormControl) {
    return direct;
  }
  for (const item of Object.values(form.controls)) {
    if (item instanceof FormGroup && item.get(name) instanceof FormControl) {
      return item.get(name) as FormControl;
    }
  }
  return null;
}

function flatten(values: Record<string, unknown>): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(values)) {
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      Object.assign(result, value as Record<string, unknown>);
    } else {
      result[key] = value;
    }
  }
  return result;
}

/** Kept for callers that import it from here; the function is in platform/atlas-errors.ts (review NG-12). */
export { problemText };
