# @pbdev/pbdev-core: what the platform uses, and the evidence

Status: level-set 2 (2026-10-27, rework R1-R6). The real library source of pbdev-core 6.0.1-0 is now available
read-only in `target-implementation-read-only/PB-DEV-COMPONENTS/Pb-common-components/` (path R below:
`Projects/Pbdev-core/Src`). The local stand-in (`WebUi/src/app/stand-in/pbdev-core/`) follows that source: the same
selectors, `@Input`/`@Output` names and types, and value behaviour. Level-set 1 (2026-10-25) was based on usages in the
target only; section 5 lists what the real source confirmed and what it changed.

## 1. Rules

1. The platform imports `@pbdev/pbdev-core` in one file only: `WebUi/src/app/platform/atlas-pbdev.ts`.
2. The platform binds only real inputs and outputs (section 2). The stand-in has no other input, so a wrong binding
   fails the local build (NG8002), as it would at the target.
3. A library control generates its own element id (`inputId` is a field, not an input). The `atlasControlId` directive
   puts `id="ctl_<Name>"` on the first `input`/`select`/`textarea` inside the control, so the form label `for` works.
4. The library controls do not convert values (`CustomControlValueAccessorDirective` binds the host `FormControl` to
   the inner element). The form model holds the value type of each control and converts to the server text
   (`atlas-form-model.ts` `toModelValue` / `toOutputValue`): Date for the date picker (`yyyy-MM-dd` on the wire),
   boolean for the checkbox (`true`/`false`), a list for a multiple ng-select (joined with `#$#`).
5. `pbdev-input`, `pbdev-select`, `pbdev-textarea` and `pbdev-date-picker` draw their own error text
   (`pbdev-input-messages` -> `pbdev-control-errors`: default texts only for `required` and `email`, else `Error`).
   The platform gives its texts as `customErrorMessages` (one text for each error key), and the form does not draw the
   text again (it stays visually hidden for `aria-describedby`).
6. Options go to `pbdev-select` and to `ng-select` as `{ label, value }` (`SelectOption`), as `transformSelect` in the
   section master.

## 2. Inventory (real source, path R)

| Class | Selector | Inputs / outputs that the platform binds | Real value | Source |
|---|---|---|---|---|
| `InputComponent<T>` | `pbdev-input` | `inputType` (required), `placeholder`, `customErrorMessages`, `formControl` | text of the native input | `components/input/input.component.ts:26-48` |
| `SelectComponent<T>` | `pbdev-select` | `options` (`SelectOption<T>[]`), `customErrorMessages`, `formControl` | the raw option value | source not supplied; API from `Src/Stories/Components/Form/select.component.stories.ts` and `select-alt` |
| `SelectOption<T>` | type | `{ label, value: T }` | | `select-alt.component.ts:38` |
| `TextareaComponent` | `pbdev-textarea` | `rows`, `customErrorMessages`, `formControl` | text | `components/textarea/textarea.component.ts:22-42` |
| `DatePickerComponent<T>` | `pbdev-date-picker` | `customErrorMessages`, `formControl` | JS `Date` (PrimeNG `p-date-picker`) | `components/date-picker/date-picker.component.ts:22-38` |
| `CheckboxComponent<T>` | `pbdev-checkbox` | `inputLabel`, `formControl` | boolean (native checkbox) | `components/checkbox/checkbox.component.ts:20-36` |
| `SectionComponent` | `pbdev-section` | `title`, `variant` (`'blue'`, as the section master) | | `components/section/section.component.ts:24-33` |
| `LoaderComponent` | `pbdev-loader` | none | | `components/loader/loader.component.ts:10-17` |
| `DialogComponent` | `pbdev-dialog` | `visible`, `visibleChange`, `header` | emits `visibleChange(false)` on X, Escape, mask; does not change `visible` itself | `components/dialog/dialog.component.ts:16-34` |
| `AgLoadingOverlayComponent` | `pbdev-ag-loading-overlay` | used as `loadingOverlayComponent` of the grids | | `components/ag-loading-overlay/ag-loading-overlay.component.ts` |

Not used on purpose:
- `mask` of `pbdev-input` (Maskito: `amount`, `percent`, `positiveInt`, `phone`). Decision D1 (2026-10-27): currency and
  percent fields work as the target does today. The target writes `maskF="amount"`, which is not an input of
  `pbdev-input`, so the target fields have no mask.
- `pbdev-radio`: the section master uses a plain `input type="radio"` (its `pbdev-radio` line is commented out).
- `pbdev-select-alt`, `pbdev-input-autocomplete(-alt)`: decision D3 keeps `ng-select` as the section master.
- `<pbdev-section-label>` is not in the library (level-set 1 named it as a fallback).

## 3. Bindings to check at the target (one build)

| Item | Where in the platform | If it is different |
|---|---|---|
| `pbdev-select` (source not supplied) has `options`, `customErrorMessages` and one inner element for `atlasControlId` | `renderers/atlas-renderers.ts` (select) | Change the select renderer; the label link is the only other effect |
| The exact pbdev-core version (target `package.json`: `^6.0.0`; source read: 6.0.1-0) | - | Compare the `*.d.ts` of the installed version with section 2 |

## 4. Styles

`platform/atlas-platform.scss` uses `@use '@pbdev/pbdev-core/styles/variables'` (the same as the target `styles.scss`)
and the classes of `pbdev-core.min.css` (`btn`, `btn--primary`, `headline-3`, `section`). The form and dashboard rules
are the rules of `cp-atlas-sectionmaster.scss` and `cp-atlas-dashboard.scss`, under the `atlas-form` and
`atlas-dashboard` hosts. Locally the stand-in `src/app/stand-in/sass/@pbdev/pbdev-core/styles/variables` gives the same
Sass entry (`stylePreprocessorOptions.includePaths`); at the target the package gives it.

## 5. Level-set 1 compared with the real source

| Level-set 1 item | Real source | Change made |
|---|---|---|
| `pbdev-input` `inputId` input | A field (generated id), not an input: NG8002 at the target | Text renderer uses `atlasControlId` (R1) |
| `pbdev-input` `maskF` | No such input (the target attribute does nothing) | Not used (D1) |
| Stand-in converts each value to a string | The controls bind the host control to the inner element | Form model converts Date/boolean/list (R2) |
| The form draws each error text | The controls draw it (`pbdev-input-messages`) | `customErrorMessages`; form text hidden for those types (R2) |
| `DialogComponent` emits `visibleChange(false)` | Confirmed | none |
| `SectionComponent.title` | Confirmed (`<p class="section-header--title">`) | `variant="blue"` added (R3) |
| `pbdev-input` `placeholder` | Confirmed | none |
| `{ label, value }` options | Confirmed (`value` is generic) | none |
