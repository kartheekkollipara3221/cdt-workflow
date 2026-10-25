# @pbdev/pbdev-core: what the platform uses, and the evidence

Status: level-set 1 (2026-10-25, review finding NG-01). The real library is not in this repository. This file lists each
class, selector, input and output that the platform uses, and the target file that proves it. The local stand-in
(`WebUi/src/app/stand-in/pbdev-core/`) has only these members, so the platform cannot depend on a member that the target
does not use.

## 1. Rules

1. The platform imports `@pbdev/pbdev-core` in one file only: `WebUi/src/app/platform/atlas-pbdev.ts`.
2. The platform binds only inputs and outputs that the target WebUi already binds (section 2).
3. The platform does not depend on an id input of a library control. The `atlasControlId` directive puts `id` and `name`
   on the first `input`, `select` or `textarea` inside the control, so the form label `for="ctl_<Name>"` works.
4. Options go to `pbdev-select` as `{ label, value }` (`SelectOption`), as the target does in `transformSelect`.

## 2. Inventory (evidence from `target-implementation-read-only/WebUi/WebUi/Source/App/`)

| Class | Selector | Inputs / outputs that the platform binds | Evidence |
|---|---|---|---|
| `InputComponent` | `pbdev-input` | `inputType`, `inputId`, `placeholder`, `formControl` | `cp-atlas-sectionmaster.html.txt` (`inputType`, `maskF`, `inputId`); `Shared/Component/Cell-editors/Text-editor.component.ts.txt` (commented list: `inputName`, `inputType`, `inputLabel`, `placeholder`, `helperText`, `iconName`, `isRequiredIndicatorVisible`, `customErrorMessages`, `variant`, `mask`, `disabled`, `required`) |
| `SelectComponent` | `pbdev-select` | `options` (`SelectOption[]`), `formControl` | `cp-atlas-sectionmaster.html.txt`; `transformSelect` in `cp-atlas-sectionmaster.ts.txt:229-233` |
| `SelectOption` | type | `{ label, value }` | `cp-atlas-steppage.ts.txt:56-60` |
| `TextareaComponent` | `pbdev-textarea` | `rows`, `formControl` | `cp-atlas-sectionmaster.html.txt` |
| `DatePickerComponent` | `pbdev-date-picker` | `formControl` | `cp-atlas-sectionmaster.html.txt` |
| `CheckboxComponent` | `pbdev-checkbox` | `inputLabel`, `formControl` | `cp-atlas-steppage.txt:84` |
| `SectionComponent` | `pbdev-section` | `title` (the target also binds `variant`) | `cp-atlas-steppage.txt:127`; import in `cp-atlas-steppage.ts.txt:12` |
| `LoaderComponent` | `pbdev-loader` | none | `cp-atlas-sectionmaster.html.txt`; `app.component.html.txt` (`variant`, `widthRem`, `color`) |
| `DialogComponent` | `pbdev-dialog` | `visible`, `visibleChange` (the two halves of `[(visible)]`), `header` | `app.component.html.txt:63` |

Known in the target but not used by the platform: `InputLabelComponent`, `RadioComponent`, `FilePickerComponent`,
`SimpleFilePickerComponent`, `AgLoadingOverlayComponent`, `StepperComponent`, `StepperService`, `TextLinkComponent`,
`NavbarComponent`, `NavbarHelpComponent`, `NavbarUserMenuComponent`, `FooterComponent`.

Not used on purpose: `<pbdev-section-label>`. The current section master uses it, but the copy of its `.ts` file does
not show which class has that selector. `<pbdev-section>` with `SectionComponent` is proven by the step page.

## 3. Bindings to check at the target (one build)

| Item | Where in the platform | If it is different |
|---|---|---|
| `DialogComponent` emits `visibleChange(false)` when the user closes it | `atlas-form.component.html`, `atlas-dashboard.component.html`, `atlas-history-dialog.ts` | Change the `(visibleChange)` handlers |
| `SectionComponent.title` shows the heading | `atlas-form.component.html` | Use `<pbdev-section-label [label]>` |
| `pbdev-input` `placeholder` | `renderers/atlas-renderers.ts` (text) | Remove the binding |
| A library control has one `input`/`select`/`textarea` (for `atlasControlId`) | `renderers/atlas-renderers.ts` | Only the label link and the Chrome Issue are affected |

## 4. How to level-set with the real library

The best source is the public type declarations of the package at the target. They are text files with no code:

1. At the target WebUi, copy `node_modules/@pbdev/pbdev-core/index.d.ts` and the `*.d.ts` files under
   `node_modules/@pbdev/pbdev-core/lib/` (or the files that `index.d.ts` names) to
   `target-implementation-read-only/pbdev-core-types/`.
2. Also copy the version line of `@pbdev/pbdev-core` from `package-lock.json`.
3. Then the stand-in can match the real names, inputs (`input()` or `@Input`), outputs and value types exactly, and
   section 3 of this file becomes a list of facts.

Other options, in order of value: the package `README` or Storybook of the library team; one build of the platform
folder in the target WebUi (`ng build` shows each unknown name or input as TS2305 or NG8002).
