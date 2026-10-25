// STAND-IN: do not copy to the target. The landing page of the local host: the apps of the user (v1/Application).
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PbdevLoader } from '@pbdev/pbdev-core';
import { AtlasApiService } from '../../platform/atlas-api.service';
import { CPApplicationRow } from '../../platform/atlas-models';
import { problemText } from '../../platform/form/atlas-form.component';

@Component({
  selector: 'standin-home',
  imports: [RouterLink, PbdevLoader],
  template: `
    <h1 class="atlas-title">Applications</h1>
    <p class="atlas-muted">The apps where you (or the user you act as) have a role. Platform apps show the new form type.</p>
    @if (loading()) {
      <pbdev-loader />
    } @else if (error()) {
      <div class="atlas-alert atlas-alert-error">{{ error() }}</div>
    } @else if (apps().length === 0) {
      <div class="atlas-panel">No application. The user has no role.</div>
    } @else {
      <div class="atlas-cards">
        @for (app of apps(); track app.ID) {
          <a class="atlas-card" [routerLink]="app.HasDashboard ? ['/apps', app.Code] : null" [attr.id]="'app_' + app.Code">
            <span class="atlas-card-code">{{ app.Code }}</span>
            <span class="atlas-card-name">{{ app.Name }}</span>
            <span class="atlas-muted">Roles: {{ app.Role }} · Form: {{ app.FormType ?? 'legacy' }}</span>
          </a>
        }
      </div>
    }
  `,
})
export class HomeComponent implements OnInit {
  private readonly api = inject(AtlasApiService);
  readonly apps = signal<CPApplicationRow[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.applications().subscribe({
      next: (apps) => {
        this.apps.set(apps);
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(problemText(error));
        this.loading.set(false);
      },
    });
  }
}
