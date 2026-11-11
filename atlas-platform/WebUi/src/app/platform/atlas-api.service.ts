// COPY to the target: WebUi/src/app/platform/. Calls the P1 endpoints that take the string app code.
// The base URL and credentials come from the interceptors (at the target: apiinterceptor, winauthinterceptor).
// The URLs have no v1/ prefix, because the target apiBaseUrl is '/v1/'.
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  CPApplicationRow,
  CPApplicationSection,
  CPAtlasDashboard,
  CPAtlasPage,
  CPAuditLog,
} from './atlas-models';

interface Wrapped<T> {
  result: T;
}

/** HTTP client of the Atlas platform endpoints. Each call unwraps the `result` of the response. */
@Injectable({ providedIn: 'root' })
export class AtlasApiService {
  private readonly http = inject(HttpClient);

  /** Returns the apps of the sidebar, or an empty list. */
  applications(): Observable<CPApplicationRow[]> {
    return this.http.get<Wrapped<CPApplicationRow[]>>('Application').pipe(map((x) => x.result ?? []));
  }

  /** Returns the dashboard (grid columns and rows) of the app. */
  dashboard(code: string): Observable<CPAtlasDashboard> {
    return this.http
      .get<Wrapped<CPAtlasDashboard>>('Dashboard', { params: new HttpParams().set('code', code) })
      .pipe(map((x) => x.result));
  }

  /** Returns the form sections and the values of a record of the app. */
  section(code: string, id: number): Observable<CPApplicationSection> {
    const params = new HttpParams().set('AppCode', code).set('ID', id);
    return this.http.get<Wrapped<CPApplicationSection>>('SectionPage', { params }).pipe(map((x) => x.result));
  }

  /** Saves the page as form data (field `input`) and returns the saved page. */
  save(page: CPAtlasPage): Observable<CPAtlasPage> {
    const body = new FormData();
    body.append('input', JSON.stringify(page));
    return this.http.post<CPAtlasPage>('ManagePage', body);
  }

  /** Removes the record of the app. */
  remove(code: string, id: number): Observable<CPAtlasPage> {
    const params = new HttpParams().set('code', code).set('id', id);
    return this.http.post<CPAtlasPage>('RemovePage', null, { params });
  }

  /** Returns the audit log rows of the record, or an empty list. */
  auditLog(code: string, id: number): Observable<CPAuditLog[]> {
    const params = new HttpParams().set('appCode', code).set('refID', id);
    return this.http.get<Wrapped<CPAuditLog[]>>('GetAuditLog', { params }).pipe(map((x) => x.result ?? []));
  }
}
