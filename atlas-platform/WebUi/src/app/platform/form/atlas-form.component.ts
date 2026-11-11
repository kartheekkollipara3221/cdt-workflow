// COPY to the target: WebUi/src/app/platform/form/.
import { NgComponentOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, Type, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, switchMap, tap } from 'rxjs';
import { FormControl, FormGroup, ReactiveFormsModule, FormRecord } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DialogComponent, LoaderComponent, SectionComponent } from '../atlas-pbdev';
import { AtlasApiService } from '../atlas-api.service';
import { AtlasHistoryDialog } from '../atlas-history-dialog';
import { CPApplicationControl, CPApplicationSection, CPButton, CPPageError } from '../atlas-models';
import { ATLAS_DASHBOARD_LINK } from '../atlas-navigation';
import { AtlasNoticeService } from '../atlas-notice.service';
import { problemText } from '../atlas-errors';
import { AtlasEffectTarget, applyEffects } from './atlas-effects';
import { AtlasFormRecord, buildFormGroup, buildOutputs, computeValue, containerOf, initialFlags, isSection, isShown, shownInvalid, toModelValue, toOutputValue, valueControls } from './atlas-form-model';
import { AtlasFieldA11yDirective } from './atlas-field-a11y.directive';
import { AtlasRendererRegistry } from './atlas-renderer-registry';
import { atlasValidators, errorText } from './atlas-validators';

/** The template data of one control. The form finds it one time for each section, not at each change detection. */
export interface AtlasFieldView {
  renderer: Type<unknown>;
  /** The renderer has an input that a label can point to (label for). */
  hasInput: boolean;
  /** The pbdev control shows its own error text, so the form text is for screen readers only. */
  ownMessages: boolean;
}

/**
 * The generic platform form, on the route form/:code/:id, beside the current sectionmaster component.
 *
 * It knows no app. The controls come from the section JSON, the renderers from the registry,
 * and the rules from the server.
 */
@Component({
  selector: 'atlas-form',
  imports: [ReactiveFormsModule, NgComponentOutlet, LoaderComponent, SectionComponent, DialogComponent, AtlasHistoryDialog, AtlasFieldA11yDirective],
  templateUrl: './atlas-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush, // The state is in signals.
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
  /** True when the mandatory-field message shows, as CPAtlasConfirmation in validate() of the section master. */
  readonly invalidOpen = signal(false);
  readonly invalidMessage = 'Please make sure all the mandatory fields are filled.';

  // The route state is in signals, so OnPush and zoneless change detection see each change.
  readonly code = signal('');
  readonly id = signal(0);
  form: AtlasFormRecord = new FormRecord<FormControl | FormRecord<FormControl>>({});
  private controlsByName = new Map<string, CPApplicationControl>();
  private containers = new Map<string, string>();

  /** The view of each control of the section, by name: renderer, label target, message mode. */
  readonly views = computed(() => {
    const views = new Map<string, AtlasFieldView>();
    for (const control of this.section()?.Controls ?? []) {
      for (const item of [control, ...(control.ChildControls ?? [])]) {
        views.set(item.Name, {
          renderer: this.registry.resolve(item.Type),
          hasInput: this.registry.hasInput(item.Type),
          ownMessages: this.registry.drawsMessages(item.Type),
        });
      }
    }
    return views;
  });

  ngOnInit(): void {
    // A new route cancels the load before it. Thus a late response for record A cannot build the form of record B.
    // No response is handled after the form is destroyed (Angular error NG0911).
    this.route.paramMap
      .pipe(
        tap((params) => {
          this.code.set((params.get('code') ?? '').toUpperCase());
          this.id.set(Number(params.get('id') ?? 0) || 0);
          this.loading.set(true);
          this.loadError.set(null);
          this.pageErrors.set([]);
        }),
        switchMap(() =>
          this.api.section(this.code(), this.id()).pipe(
            catchError((error) => {
              this.loadError.set(problemText(error));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((section) => this.build(section));
  }

  private build(section: CPApplicationSection): void {
    this.controlsByName = new Map(valueControls(section).map((x) => [x.Name, x]));
    this.containers = containerOf(section);
    const flags = initialFlags(section);
    this.visible.set(flags.visible);
    this.required.set(flags.required);
    this.form = buildFormGroup(section);
    this.disableUnsupported();
    this.section.set(section);
    this.wireEffects();
    this.wireComputed(section);
    if (!this.canSave()) {
      this.form.disable({ emitEvent: false }); // The server refuses the save, so the form is read-only.
    }
    this.loading.set(false);
  }

  /** Disables each control that has no renderer. The user cannot edit it, so it is not validated or sent. */
  private disableUnsupported(): void {
    for (const control of this.controlsByName.values()) {
      if (!this.registry.has(control.Type)) {
        this.form.get(control.Name)?.disable({ emitEvent: false });
      }
    }
  }

  /** Runs the effects when the form is built, and each time the value of a source control changes. */
  private wireEffects(): void {
    const sources = [...this.controlsByName.values()].filter((x) => x.Effects?.length);
    for (const control of sources) {
      this.form.get(control.Name)?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value) => this.runEffects(control, value));
    }
    for (const control of sources) {
      applyEffects(control, toOutputValue(control, this.form.get(control.Name)?.value), this);
    }
  }

  /**
   * Updates the Computed fields each time the form value changes.
   *
   * The client shows the value while the user types. The server calculates and saves its own value.
   */
  private wireComputed(section: CPApplicationSection): void {
    if (section.Computed) {
      this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.updateComputed());
      this.updateComputed();
    }
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

  /**
   * Names of the controls whose effects run now.
   *
   * Set, Clear and SetFromOption change other controls. Then the effects of those controls also run.
   * This set stops a cycle in the config (A sets B, B sets A) after one pass.
   * The start-up check also reports a cycle.
   */
  private readonly effectsRunning = new Set<string>();

  /**
   * Runs the effects of a control for a new value.
   *
   * Does nothing when the effects of this control already run.
   */
  private runEffects(control: CPApplicationControl, value: unknown): void {
    if (this.effectsRunning.has(control.Name)) {
      return;
    }
    this.effectsRunning.add(control.Name);
    try {
      applyEffects(control, toOutputValue(control, value), this);
    } finally {
      this.effectsRunning.delete(control.Name);
    }
  }

  setValue(name: string, value: string | null): void {
    const control = this.controlsByName.get(name);
    this.form.get(name)?.setValue(control ? toModelValue(control, value) : (value ?? ''));
  }

  // ---------- Template helpers ----------

  isSection = isSection;

  /** Returns true when the control is visible. A control with no flag is visible. */
  isVisible(name: string): boolean {
    return this.visible()[name] !== false;
  }

  /** Returns true when the control and its section are visible. */
  isShown(name: string): boolean {
    return isShown(name, this.containers, (x) => this.isVisible(x));
  }

  isRequired(name: string): boolean {
    return !!this.required()[name];
  }

  /**
   * Returns the error text of a control, or null.
   *
   * Shows an error only after the user touches the control, or when the server sent an error for it.
   */
  error(control: CPApplicationControl): string | null {
    const item = this.form.get(control.Name);
    if (!item || item instanceof FormGroup || !(item.touched || item.errors?.['server'])) {
      return null;
    }
    return errorText(item.errors, control.Label, control.Validator);
  }

  /**
   * The buttons that show. Delete shows only for a saved record when the user can delete.
   * Save shows only when the user can save.
   */
  readonly visibleButtons = computed<CPButton[]>(() =>
    (this.section()?.Buttons ?? []).filter((x) => {
      if (!x.Visible) {
        return false;
      }
      if (actionOf(x) === 'delete') {
        return this.id() > 0 && !!this.section()?.UserPermissions?.CanDelete;
      }
      if (actionOf(x) === 'save') {
        return this.canSave();
      }
      return true;
    }),
  );

  /**
   * True when the user can save the record.
   *
   * When CanEdit (or CanCreate for a new record) is false, the server refuses the save.
   * Then the form is read-only and has no Save button, as in the target section master.
   * When the server sends no UserPermissions (an older server), the Save button shows.
   */
  readonly canSave = computed(() => {
    const permissions = this.section()?.UserPermissions;
    return this.id() > 0 ? permissions?.CanEdit !== false : permissions?.CanCreate !== false;
  });

  readonly canViewHistory = computed(() => this.id() > 0 && !!this.section()?.UserPermissions?.CanViewHistory);

  // ---------- Actions ----------

  /** Runs the action of a button: Save, Delete (after a confirmation) or else close. */
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

  /** Goes back to the dashboard of the app. */
  close(): void {
    this.router.navigate(this.dashboardLink(this.code()));
  }

  /**
   * Validates the shown fields and saves the record.
   *
   * When a shown field is not valid, shows the mandatory-field message and does not save.
   */
  save(): void {
    const section = this.section();
    if (!section || this.saving()) {
      return;
    }
    // Touch only the shown fields. Thus a field that an effect shows later has no error before the user types.
    for (const control of valueControls(section)) {
      if (this.isShown(control.Name)) {
        this.form.get(control.Name)?.markAllAsTouched();
      }
    }
    this.pageErrors.set([]);
    if (shownInvalid(section, this.form, (name) => this.isShown(name))) {
      // As in the section master, show the message dialog. The fields also show their errors, because all are touched.
      this.invalidOpen.set(true);
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
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.saving.set(false);
          if (result.Errors?.length) {
            this.showErrors(result.Errors);
            return;
          }
          this.notice.show('success', 'Record saved successfully');
          this.router.navigate(this.dashboardLink(this.code()));
        },
        error: (error) => {
          this.saving.set(false);
          this.httpFail(error);
        },
      });
  }

  /** Deletes the record and goes back to the dashboard. Errors show at the top. */
  remove(): void {
    this.confirmDelete.set(false);
    this.api.remove(this.code(), this.id()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        if (result.Errors?.length) {
          this.pageErrors.set(result.Errors);
          return;
        }
        this.notice.show('success', 'Record deleted successfully');
        this.router.navigate(this.dashboardLink(this.code()));
      },
      error: (error) => this.httpFail(error),
    });
  }

  /** Shows an HTTP failure of a save or a delete as one error at the top. */
  private httpFail(error: unknown): void {
    this.pageErrors.set([{ Field: '', Code: 'Http', Message: problemText(error) }]);
  }

  /**
   * Shows the server errors of a save.
   *
   * An error of an enabled field shows at that field. The other errors show at the top.
   */
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

/** Returns a control by name from the form or from a grid row group, or null. */
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

