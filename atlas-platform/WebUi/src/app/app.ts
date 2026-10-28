// STAND-IN shell: do not copy to the target (the target has its own app component, menu and toastr).
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AtlasApiService } from './platform/atlas-api.service';
import { CPApplicationRow } from './platform/atlas-models';
import { ACT_AS_KEY, readActAs } from './stand-in/shell/stand-in.interceptor';

interface TestUser {
  LanId: string;
  DisplayName: string;
  Note?: string | null;
}

interface CurrentUser {
  LanID: string;
  DisplayName: string;
  Roles?: string[] | null;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly api = inject(AtlasApiService);

  readonly user = signal<CurrentUser | null>(null);
  readonly testUsers = signal<TestUser[]>([]);
  readonly apps = signal<CPApplicationRow[]>([]);
  readonly actAs = signal(readActAs());
  readonly apiDown = signal(false);

  ngOnInit(): void {
    this.http.get<CurrentUser>('v1/authorize').subscribe({
      next: (user) => this.user.set(user),
      error: () => this.apiDown.set(true),
    });
    this.http.get<{ result: TestUser[] }>('v1/standin/users').subscribe({ next: (x) => this.testUsers.set(x.result ?? []) });
    this.api.applications().subscribe({ next: (apps) => this.apps.set(apps.filter((x) => x.HasDashboard)) });
  }

  changeUser(lanId: string): void {
    try {
      if (lanId) {
        localStorage.setItem(ACT_AS_KEY, lanId);
      } else {
        localStorage.removeItem(ACT_AS_KEY);
      }
    } catch {
      // storage blocked: the switch has no effect
    }
    window.location.assign('/');
  }
}
