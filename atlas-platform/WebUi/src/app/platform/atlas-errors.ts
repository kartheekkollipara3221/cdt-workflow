// COPY to the target: WebUi/src/app/platform/. The user text of an HTTP error (review NG-12): its own file, so the
// dashboard and the form chunks do not share the form component code.
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
