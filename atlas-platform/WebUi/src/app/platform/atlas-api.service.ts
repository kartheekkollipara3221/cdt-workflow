// COPY to the target: WebUi/src/app/platform/. The P1 endpoints with the string app code (spec 07, 4.4).
// The base URL and credentials come from the interceptors (at the target: apiinterceptor, winauthinterceptor).
// The URLs have no v1/ prefix: the target apiBaseUrl is '/v1/' (review 3 RV-01).
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

@Injectable({ providedIn: 'root' })
export class AtlasApiService {
  private readonly http = inject(HttpClient);

  applications(): Observable<CPApplicationRow[]> {
    return this.http.get<Wrapped<CPApplicationRow[]>>('Application').pipe(map((x) => x.result ?? []));
  }

  dashboard(code: string): Observable<CPAtlasDashboard> {
    return this.http
      .get<Wrapped<CPAtlasDashboard>>('Dashboard', { params: new HttpParams().set('code', code) })
      .pipe(map((x) => x.result));
  }

  section(code: string, id: number): Observable<CPApplicationSection> {
    const params = new HttpParams().set('AppCode', code).set('ID', id);
    return this.http.get<Wrapped<CPApplicationSection>>('SectionPage', { params }).pipe(map((x) => x.result));
  }

  save(page: CPAtlasPage): Observable<CPAtlasPage> {
    const body = new FormData();
    body.append('input', JSON.stringify(page));
    return this.http.post<CPAtlasPage>('ManagePage', body);
  }

  remove(code: string, id: number): Observable<CPAtlasPage> {
    const params = new HttpParams().set('code', code).set('id', id);
    return this.http.post<CPAtlasPage>('RemovePage', null, { params });
  }

  auditLog(code: string, id: number): Observable<CPAuditLog[]> {
    const params = new HttpParams().set('appCode', code).set('refID', id);
    return this.http.get<Wrapped<CPAuditLog[]>>('GetAuditLog', { params }).pipe(map((x) => x.result ?? []));
  }
}
