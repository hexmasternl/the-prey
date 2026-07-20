import { Routes } from '@angular/router';
import { authGuardFn } from '@auth0/auth0-angular';
import { locationConsentGuard } from './core/location-consent.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./login/login.page').then((m) => m.LoginPage),
  },
  {
    // Non-dismissable fallback for a declined disclosure on iOS/web (see
    // locationConsentGuard) — intentionally unguarded, it IS the gate's own escape hatch.
    path: 'consent-required',
    loadComponent: () =>
      import('./consent-required/consent-required.page').then((m) => m.ConsentRequiredPage),
  },
  {
    path: 'home',
    loadComponent: () => import('./home/home.page').then((m) => m.HomePage),
    canActivate: [authGuardFn, locationConsentGuard],
  },
  {
    path: 'play',
    loadComponent: () => import('./home/home.page').then((m) => m.HomePage),
    canActivate: [authGuardFn],
  },
  {
    path: 'playfields',
    loadComponent: () =>
      import('./playfields/playfields-list.page').then((m) => m.PlayfieldsListPage),
    canActivate: [authGuardFn],
  },
  {
    path: 'playfields/new',
    loadComponent: () =>
      import('./playfields/playfield-create.page').then((m) => m.PlayfieldCreatePage),
    canActivate: [authGuardFn],
  },
  {
    path: 'playfields/:id',
    loadComponent: () =>
      import('./playfields/playfield-detail.page').then((m) => m.PlayfieldDetailPage),
    canActivate: [authGuardFn],
  },
  {
    path: 'playfields/:id/area',
    loadComponent: () =>
      import('./playfields/area/playfield-area.page').then((m) => m.PlayfieldAreaPage),
    canActivate: [authGuardFn],
  },
  {
    path: 'games/create',
    loadComponent: () =>
      import('./games/game-create.page').then((m) => m.GameCreatePage),
    canActivate: [authGuardFn],
  },
  {
    // No authGuardFn here: the join page is the entry point for shared deep links,
    // so it restores the session itself and redirects to /login (preserving the
    // join target) when there is none, instead of the guard's default redirect.
    // locationConsentGuard still applies — a deep link must not bypass the disclosure.
    path: 'games/join',
    loadComponent: () =>
      import('./games/game-join.page').then((m) => m.GameJoinPage),
    canActivate: [locationConsentGuard],
  },
  {
    path: 'games/:id/lobby',
    loadComponent: () =>
      import('./games/game-lobby.page').then((m) => m.GameLobbyPage),
    canActivate: [authGuardFn, locationConsentGuard],
  },
  {
    path: 'games/:id/play',
    loadComponent: () =>
      import('./games/game-prey.page').then((m) => m.GamePreyPage),
    canActivate: [authGuardFn, locationConsentGuard],
  },
  {
    path: 'games/:id/hunt',
    loadComponent: () =>
      import('./games/game-hunter.page').then((m) => m.GameHunterPage),
    canActivate: [authGuardFn, locationConsentGuard],
  },
  {
    path: 'games/:id/outcome',
    loadComponent: () =>
      import('./games/game-outcome.page').then((m) => m.GameOutcomePage),
    canActivate: [authGuardFn],
  },
  {
    path: 'settings',
    loadComponent: () => import('./settings/settings.page').then((m) => m.SettingsPage),
    canActivate: [authGuardFn],
  },
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  },
];
