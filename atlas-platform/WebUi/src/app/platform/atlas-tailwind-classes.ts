// COPY to the target: WebUi/Source/App/platform/. The target Tailwind config scans "./src/**/*.{html,ts}" only, so a
// class that is only in a server JSON file (DivClass, Class) is not generated. Tailwind finds each class name below in
// this file and generates it. When an app JSON uses a new Tailwind class, add it here (or to "safelist" in
// tailwind.config.js). The list is not used at run time.
export const ATLAS_TAILWIND_CLASSES = [
  'grid',
  'grid-cols-2',
  'grid-cols-3',
  'col-span-2',
  'col-span-3',
  'gap-6',
  'items-start',
  'p-8',
  'p-16',
  'bg-white',
] as const;
