// STAND-IN: do not copy to the target. The target has apiinterceptor (base URL) and winauthinterceptor
// (withCredentials). The local "act as" header works only with the Development API.
import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';

/** The localStorage key of the LAN ID of the test user to act as. */
export const ACT_AS_KEY = 'atlas.standin.actAs';

/** Returns the LAN ID of the test user to act as, or an empty text when storage is empty or blocked. */
export function readActAs(): string {
  try {
    return localStorage.getItem(ACT_AS_KEY) ?? '';
  } catch {
    return '';
  }
}

/**
 * Adds the API base URL to a relative URL, sends the credentials, and adds the X-Atlas-Act-As header when set.
 */
export const standInInterceptor: HttpInterceptorFn = (req, next) => {
  const actAs = readActAs();
  const url = /^https?:/.test(req.url) ? req.url : environment.apiBaseUrl + req.url;
  return next(
    req.clone({
      url,
      withCredentials: true,
      setHeaders: actAs ? { 'X-Atlas-Act-As': actAs } : {},
    }),
  );
};
