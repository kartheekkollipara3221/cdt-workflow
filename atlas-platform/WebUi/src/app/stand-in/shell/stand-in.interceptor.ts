// STAND-IN: do not copy to the target. The target has apiinterceptor (base URL) and winauthinterceptor
// (withCredentials). The local "act as" header works only with the Development API (spec 12, 11.3).
import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export const ACT_AS_KEY = 'atlas.standin.actAs';

export function readActAs(): string {
  try {
    return localStorage.getItem(ACT_AS_KEY) ?? '';
  } catch {
    return '';
  }
}

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
