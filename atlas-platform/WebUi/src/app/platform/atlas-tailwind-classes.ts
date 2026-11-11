// COPY to the target: WebUi/src/app/platform/.

/**
 * Tailwind classes that the app JSON files use (DivClass, Class).
 *
 * The target Tailwind config scans only the .html and .ts files under ./src. Thus it does not generate a class that
 * is only in a server JSON file. Tailwind finds each class name below and generates it. When an app JSON uses a new Tailwind
 * class, add it here (or to "safelist" in tailwind.config.js). The code does not use the list at run time.
 */
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
