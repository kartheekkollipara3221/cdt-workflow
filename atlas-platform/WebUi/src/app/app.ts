// STAND-IN shell: do not copy to the target. It has the layout of the target app.component (navbar, sidebar from
// v1/Application, home-wrapper, footer, logout dialog), so the local preview looks like the target. Local only:
// the user menu also has "Act as" items for the test users (no Windows sign-in needed).
import { NgClass } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import {
  DialogComponent,
  FooterComponent,
  LoaderComponent,
  MenuItem,
  NavbarComponent,
  NavbarHelpComponent,
  NavbarUserMenuComponent,
} from '@pbdev/pbdev-core';
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

/** A sidebar item, as the target builds it in loadApplication(). */
export interface ShellMenuItem {
  label: string;
  isOpen: boolean;
  icon: string;
  url?: string;
  children?: { icon: string; label: string; path: string }[];
}

/** The sidebar items of the target loadApplication(): one item for each app, the link is dashboard/<Code>. */
export function shellMenuItems(apps: CPApplicationRow[]): ShellMenuItem[] {
  return apps
    .filter((app) => app.HasDashboard !== false)
    .map((app) => ({ label: app.Name, isOpen: false, icon: 'fas fa-home', url: 'dashboard/' + app.Code }));
}

/** Root component of the local stand-in shell: navbar, sidebar, page area and footer. */
@Component({
  selector: 'app-root',
  imports: [
    NgClass,
    RouterLink,
    RouterOutlet,
    NavbarComponent,
    NavbarHelpComponent,
    NavbarUserMenuComponent,
    DialogComponent,
    LoaderComponent,
    FooterComponent,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly api = inject(AtlasApiService);

  readonly title = 'CP Atlas';
  readonly helpTitle = 'Ask help';
  readonly supportEmail = 'cp-atlas-support@example.com';
  readonly version = '0.0.0-local';
  readonly reducePadding = true;
  isSidebarCollapsed = false;
  dialogVisible = false;

  readonly loadingMenu = signal(true);
  readonly user = signal<CurrentUser | null>(null);
  readonly testUsers = signal<TestUser[]>([]);
  readonly menuItems = signal<ShellMenuItem[]>([]);
  readonly actAs = signal(readActAs());
  readonly apiDown = signal(false);

  readonly userName = computed(() => this.user()?.DisplayName ?? ' ');
  readonly subtitle = computed(() => this.user()?.Roles?.join(', ') || 'no role');
  readonly userMenuItems = computed<MenuItem[]>(() => [
    ...this.testUsers().map((t) => ({
      label: (t.LanId === this.actAs() ? '✓ ' : '') + 'Act as ' + t.DisplayName,
      command: () => this.changeUser(t.LanId),
    })),
    ...(this.actAs() ? [{ label: 'Act as me (Windows login)', command: () => this.changeUser('') }] : []),
    { label: 'Log out', styleClass: 'p-menuitem-bold', command: () => this.showDialog() },
  ]);

  /** Loads the current user, the test users and the sidebar apps. Shows the API banner when a call fails. */
  ngOnInit(): void {
    this.http.get<CurrentUser>('authorize').subscribe({
      next: (user) => this.user.set(user),
      error: () => this.apiDown.set(true),
    });
    this.http.get<{ result: TestUser[] }>('standin/users').subscribe({ next: (x) => this.testUsers.set(x.result ?? []) });
    this.api.applications().subscribe({
      next: (apps) => {
        this.menuItems.set(shellMenuItems(apps));
        this.loadingMenu.set(false);
      },
      error: () => {
        this.apiDown.set(true);
        this.loadingMenu.set(false);
      },
    });
  }

  /** Opens or closes the submenu of a sidebar item that has children. */
  toggleMenuItem(item: ShellMenuItem): void {
    if (!this.isSidebarCollapsed && item.children) {
      item.isOpen = !item.isOpen;
    }
  }

  /** Shows the logout dialog. */
  showDialog(): void {
    this.dialogVisible = true;
  }

  /** Hides the logout dialog. */
  hideDialog(): void {
    this.dialogVisible = false;
  }

  /** Closes the logout dialog. The target goes to environment.redirectLogoutURL instead. */
  logout(): void {
    this.dialogVisible = false;
  }

  /**
   * Acts as the given test user and reloads the app.
   *
   * @param lanId The LAN ID of the test user. An empty value goes back to the Windows login.
   */
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
