// COPY to the target: WebUi/src/app/platform/. This helper has its own file. Thus the dashboard chunk and the form
// chunk do not share the form component code.

/**
 * Returns the text for the user of an HTTP error.
 *
 * The text comes from the problem details (detail, then title) of the response. Status 403, 404 and 0 get their own
 * text.
 */
export function problemText(error: unknown): string {
  const response = error as { status?: number; error?: { detail?: string; title?: string } };
  if (response?.status === 403) {
    return response.error?.detail ? `Access denied: ${response.error.detail}` : 'Access denied.';
  }
  if (response?.status === 404) {
    return response.error?.detail ?? 'Not found.';
  }
  if (response?.status === 0) {
    return 'The service is not available. Try again later.';
  }
  return response?.error?.detail ?? response?.error?.title ?? 'The request failed.';
}
