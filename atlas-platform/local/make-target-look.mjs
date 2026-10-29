// LOCAL ONLY: do not copy to the target. Builds the "target look" for the local preview from the read-only copy
// of the target code (target-implementation-read-only, not in git). Output: atlas-platform/WebUi/target-look/
// (git-ignored). No target code goes into git.
//
//   node atlas-platform/local/make-target-look.mjs [--source <target-implementation-read-only>]
//   cd atlas-platform/WebUi; npm run start:target-look
//
// What it makes:
// - target-look/pbdev-core: the real pbdev-core source used by the platform and the shell (.txt -> real names).
//   pbdev-select is not supplied: the pbdev-select-alt source (PrimeNG p-select) stands in for it.
// - target-look/sass/@pbdev/pbdev-core/styles: the real variables, pbdev-core.min.css (compiled from the real
//   Sass) and ag-theme-pbdev.min.css, at the paths the target styles.scss uses.
// - target-look/theming: the target Tailwind theme (colors.js, fonts.js, custom-base/components/utilities).
// - target-look/styles.scss: the target src/styles.scss, unchanged except the font line (Mulish from
//   @fontsource/mulish: the target font files are not supplied) and the theming path.
// - target-look/look.providers.ts: provideAnimations() and providePrimeNG(Lara), as the target app.config.ts.
//
// Known gaps (transcription, not platform): the ag-grid pbdev icon font and one svg are UNREADABLE (the grid uses
// the quartz icon font, same glyphs); dialog.component.scss is not supplied; Logo-white.png is not supplied.
import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, '..', '..');
const webUi = resolve(here, '..', 'WebUi');
const argSource = process.argv.indexOf('--source');
const source = resolve(argSource > 0 ? process.argv[argSource + 1] : join(repo, 'target-implementation-read-only'));
const lib = join(source, 'PB-DEV-COMPONENTS', 'Pb-common-components', 'Projects', 'Pbdev-core');
const app = join(source, 'WebUi', 'WebUi', 'Source');
const out = join(webUi, 'target-look');
const core = join(out, 'pbdev-core');
const sassStyles = join(out, 'sass', '@pbdev', 'pbdev-core', 'styles');

for (const p of [lib, app]) {
  if (!existsSync(p)) fail(`Not found: ${p}. Pass --source <target-implementation-read-only>.`);
}
if (!existsSync(join(webUi, 'node_modules', 'sass'))) fail('Run npm install in atlas-platform/WebUi first.');

const read = (p) => readFileSync(p, 'utf8').replace(/\r\n/g, '\n');
function write(p, text) {
  mkdirSync(dirname(p), { recursive: true });
  writeFileSync(p, text);
}
function fail(message) {
  console.error(`make-target-look: ${message}`);
  process.exit(1);
}
/** Copies one transcribed file (name.ext.txt) to its real name. */
function copyTxt(from, to, edit = (t) => t) {
  if (!existsSync(from)) fail(`Not found: ${from}`);
  write(to, edit(read(from)));
}
/** Copies a transcribed component folder (recursive), without the spec files. */
function copyDir(from, to) {
  for (const name of readdirSync(from)) {
    const f = join(from, name);
    if (statSync(f).isDirectory()) copyDir(f, join(to, name));
    else if (name.endsWith('.txt') && !name.includes('.spec.')) copyTxt(f, join(to, name.slice(0, -4)));
  }
}

rmSync(out, { recursive: true, force: true });
const src = (...p) => join(lib, 'Src', ...p);
const dest = (...p) => join(core, 'src', ...p);

// 1. Classes and the value-accessor directive.
copyTxt(src('Classes', 'custom-validators', 'custom-validators.class.ts.txt'), dest('classes', 'custom-validators', 'custom-validators.class.ts'));
copyTxt(src('Classes', 'Default-input-masks', 'default-input-masks.ts.txt'), dest('classes', 'default-input-masks', 'default-input-masks.ts'));
copyTxt(
  src('directives', 'custom-control-value-accessor-files', 'custom-control-value-accessor.directive.ts.txt'),
  dest('directives', 'custom-control-value-accessor', 'custom-control-value-accessor.directive.ts'),
);

// 2. Components: source folder -> folder of the published library.
const components = {
  'ag-loading-overlay': 'ag-loading-overlay',
  Checkbox: 'checkbox',
  'control-errors': 'control-errors',
  'data-picker': 'date-picker',
  dialog: 'dialog',
  input: 'input',
  'loader-files': 'loader',
  'section-component-files': 'section',
  textarea: 'textarea',
  tooltip: 'tooltip',
  navbar: 'navbar',
  Footer: 'footer',
};
for (const [from, to] of Object.entries(components)) copyDir(src('Components', from), dest('components', to));
// Transcription gaps: the template binds Date[] | undefined to a Date[] input; dialog.component.scss is missing.
copyTxt(src('Components', 'data-picker', 'date-picker.component.html.txt'), dest('components', 'date-picker', 'date-picker.component.html'),
  (t) => t.replace('[disabledDates]="disabledDates"', '[disabledDates]="disabledDates ?? []"'));
if (!existsSync(dest('components', 'dialog', 'dialog.component.scss'))) {
  write(dest('components', 'dialog', 'dialog.component.scss'), '/* not supplied */\n');
}

// pbdev-select source is not supplied. pbdev-select-alt (same library, same inputs, PrimeNG p-select) stands in
// for it under the pbdev-select selector, so the selects look like the PrimeNG selects of the target.
const rename = (t) => t.replace(/SelectAltComponent/g, 'SelectComponent').replace("selector: 'pbdev-select-alt'", "selector: 'pbdev-select'")
  .replace(/select-alt\.component/g, 'select.component');
for (const ext of ['ts', 'html', 'scss']) {
  copyTxt(src('Components', 'select-alt-files', `select-alt.component.${ext}.txt`), dest('components', 'select', `select.component.${ext}`), rename);
}
write(dest('components', 'select', 'select-option.interface.ts'),
  `// Not supplied: the shape the platform uses (label/value).
export interface SelectOption<T = unknown> {
  label: string;
  value: T;
}
`);

// The navbar needs the side-nav service and animations (supplied as a zip). Windows tar reads zip files.
const zip = src('Components', 'side-nav-item', 'side-nav-files.zip');
const unzipped = mkdtempSync(join(tmpdir(), 'side-nav-'));
const tar = process.platform === 'win32' ? join(process.env.SystemRoot ?? 'C:\\Windows', 'System32', 'tar.exe') : 'unzip';
execFileSync(tar, process.platform === 'win32' ? ['-xf', zip, '-C', unzipped] : ['-q', zip, '-d', unzipped]);
for (const name of ['side-nav.service.ts', 'animations.ts', 'constants.ts']) {
  copyTxt(join(unzipped, `${name}.txt`), dest('components', 'side-nav', name));
}
rmSync(unzipped, { recursive: true, force: true });

// 3. Global Sass partials: source name -> _name.scss of the library.
const partials = {
  'autocomplete.scss.txt': 'autocomplete', 'button.scss.txt': 'button', 'chip.scss.txt': 'chip',
  'datepicker.scss.txt': 'date-picker', 'diamonds.scss.txt': 'diamonds-page-background',
  'dropdown.scss.txt': 'dropdown', 'menu.scss.txt': 'menu', 'Model.scss.txt': 'modal',
  'navbar.scss.txt': 'navbar-item', 'overlay-panel_scss.txt': 'overlay-panel', 'progressbar.scss.txt': 'progressbar',
  'selectbutton.scss.txt': 'select-button', 'Steps.scss.txt': 'steps', 'switch.scss.txt': 'switch',
  'toast.scss.txt': 'toast', 'tooltip.scss.txt': 'tooltip',
};
// Some base64 images are cut off in the transcription (no closing quote): drop those declarations.
const dropCutData = (t) =>
  t.replace(/^(\s*)[\w-]+:\s*url\(['"]?data:(?![^\n]*['"]?\)\s*;?\s*$)[^\n]*$/gm, '$1/* base64 image cut off in the transcription */');
for (const [from, to] of Object.entries(partials)) {
  copyTxt(src('Components', from), dest('components', `_${to}.scss`), dropCutData);
}

// 4. Styles: variables, icons, pbdev-core.scss, ag-grid theme.
const styles = join(core, 'styles');
for (const [from, to] of Object.entries({
  '_index.scss.txt': '_index.scss', 'colors.scss.txt': '_colors.scss', 'constants.scss.txt': '_constants.scss',
  'icons.scss.txt': '_icons.scss', 'typography.scss.txt': '_typography.scss',
})) copyTxt(join(lib, 'styles', 'Variables', from), join(styles, 'variables', to));
copyTxt(join(lib, 'styles', 'icons_scss.txt'), join(styles, '_icons.scss'));
copyTxt(join(lib, 'styles', 'pbdev-core.scss.txt'), join(styles, 'pbdev-core.scss'));
cpSync(join(styles, 'variables'), join(sassStyles, 'variables'), { recursive: true });

const requireWebUi = createRequire(join(webUi, 'package.json'));
const sass = requireWebUi('sass');
const compiled = sass.compile(join(styles, 'pbdev-core.scss'), {
  style: 'compressed',
  loadPaths: [join(webUi, 'node_modules')],
  silenceDeprecations: ['import', 'global-builtin', 'color-functions', 'mixed-decls'],
  logger: sass.Logger.silent,
});
write(join(sassStyles, 'pbdev-core.min.css'), compiled.css);

// The ag-grid theme: the base64 icon font and one svg are UNREADABLE in the transcription. Drop both and use the
// quartz icon font (loaded by the target styles.scss too; ag-grid icon fonts share the glyph codes).
copyTxt(join(lib, 'styles', 'ag-theme-pbdev.css.txt'), join(sassStyles, 'ag-theme-pbdev.min.css'), (t) =>
  t
    .replace(/@font-face\s*\{[^}]*UNREADABLE[^}]*\}/, '/* agGridPbdev icon font: UNREADABLE in the transcription */')
    .replace(/^.*UNREADABLE.*$/gm, '  /* UNREADABLE svg removed */')
    .replace(/agGridPbdev/g, 'agGridQuartz'),
);

// 5. Tailwind theme of the target app.
const theming = join(app, 'App', 'Theming');
for (const name of readdirSync(theming).filter((n) => n.endsWith('.txt'))) {
  copyTxt(join(theming, name), join(out, 'theming', name.slice(0, -4)));
}
if (!existsSync(join(out, 'theming', 'fonts.js'))) {
  // Only the older copy (Source/Source) has fonts.js.
  copyTxt(join(source, 'Source', 'Source', 'App', 'Theming', 'fonts.js.txt'), join(out, 'theming', 'fonts.js'));
}
for (const name of ['colors.js', 'fonts.js']) {
  // The transcribed files assign a global (colors = {...}); make them CommonJS modules for tailwind.config.js.
  const p = join(out, 'theming', name);
  const text = read(p);
  if (!text.includes('module.exports')) {
    const variable = text.match(/^\s*(?:const|let|var)?\s*(\w+)\s*=/m)?.[1];
    write(p, `${text}\nmodule.exports = ${variable};\n`);
  }
}

// 6. The target styles.scss.
const targetStyles = read(join(app, 'styles.scss.txt'));
// The pbdev font files (Mulish, Cormorant Infant, pbdev-icons) are not supplied. Mulish and Cormorant Infant are
// open fonts (@fontsource). pbdev-icons is a private icon font: the icons in use get a Font Awesome glyph.
const fonts = [
  ...['300', '300-italic', '400', '400-italic', '500', '500-italic', '600', '600-italic', '700', '700-italic', '800']
    .map((w) => `@use '@fontsource/mulish/${w}.css' as mulish-${w};`),
  ...['400', '400-italic', '500', '500-italic', '600', '600-italic']
    .map((w) => `@use '@fontsource/cormorant-infant/${w}.css' as cormorant-${w};`),
].join('\n');
const iconFallback = {
  user: 'f007', help: 'f059', 'chevron-down': 'f078', calendar: 'f133', 'caret-right': 'f0da', close: 'f00d',
  'info-outline': 'f05a', 'more-horizontal': 'f141',
};
const icons = `\n// LOCAL ONLY: pbdev-icons font not supplied. Font Awesome glyphs for the pbdev icons in use.\n${Object.entries(iconFallback)
  .map(([n, c]) => `.pbdev_icon-${n}:before { content: '\\${c}'; font-family: 'Font Awesome 7 Free' !important; font-weight: 900; }`)
  .join('\n')}\n`;
const styled = targetStyles
  .replace("@use '@pbdev/pbdev-core/styles/fonts.min.css';", `// target: @use '@pbdev/pbdev-core/styles/fonts.min.css'; (font files not supplied)\n${fonts}`)
  .replace(/@use 'app\/theming\//g, "@use 'theming/");
if (styled === targetStyles) fail('styles.scss: the font or theming line was not found.');
write(join(out, 'styles.scss'), `// GENERATED by atlas-platform/local/make-target-look.mjs from the target src/styles.scss. Do not edit.\n${styled}${icons}`);

// 7. The library entry point and the host providers.
write(join(core, 'index.ts'), `// GENERATED by make-target-look.mjs. Real pbdev-core source (subset the platform and the shell use).
export * from './src/components/ag-loading-overlay/ag-loading-overlay.component';
export * from './src/components/checkbox/checkbox.component';
export * from './src/components/control-errors/control-errors.component';
export * from './src/components/date-picker/date-picker.component';
export * from './src/components/dialog/dialog.component';
export * from './src/components/footer/footer.component';
export * from './src/components/input/input.component';
export * from './src/components/input/input-label/input-label.component';
export * from './src/components/input/input-messages/input-messages.component';
export * from './src/components/loader/loader.component';
export * from './src/components/navbar/navbar.component';
export * from './src/components/navbar/navbar-help/navbar-help.component';
export * from './src/components/navbar/navbar-user-menu/navbar-user-menu.component';
export * from './src/components/section/section.component';
export * from './src/components/side-nav/side-nav.service';
export * from './src/components/textarea/textarea.component';
export * from './src/components/tooltip/tooltip.component';
export * from './src/components/tooltip/tooltip.interface';
export * from './src/directives/custom-control-value-accessor/custom-control-value-accessor.directive';
export * from './src/classes/custom-validators/custom-validators.class';
export * from './src/classes/default-input-masks/default-input-masks';
// The target imports MenuItem from primeng/api; the stand-in shell imports it from here.
export type { MenuItem } from 'primeng/api';
// pbdev-select is not supplied: pbdev-select-alt source under the pbdev-select selector.
export * from './src/components/select/select.component';
export * from './src/components/select/select-option.interface';
`);
write(join(out, 'look.providers.ts'), `// GENERATED by make-target-look.mjs. As the target app.config.ts: animations and PrimeNG (Lara, no dark mode).
import { EnvironmentProviders, Provider } from '@angular/core';
import { provideAnimations } from '@angular/platform-browser/animations';
import Lara from '@primeng/themes/lara';
import { providePrimeNG } from 'primeng/config';

export const lookProviders: (Provider | EnvironmentProviders)[] = [
  provideAnimations(),
  providePrimeNG({ theme: { preset: Lara, options: { darkModeSelector: false } }, ripple: false }),
];
`);

console.log(`make-target-look: done -> ${out}`);
console.log('Next: cd atlas-platform/WebUi; npm run start:target-look');
