// COPY to the target: WebUi/src/app/platform/. Small value helpers that the form and the dashboard share.

/** True for null and undefined. */
export function isNil(value: unknown): value is null | undefined {
  return value === null || value === undefined;
}

/**
 * The local day of a text that starts with yyyy-MM-dd, plus addDays (not UTC, so the day is the same in each time
 * zone). Null: the text has no date.
 */
export function parseLocalDay(text: unknown, addDays = 0): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(text ?? ''));
  return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]) + addDays) : null;
}
