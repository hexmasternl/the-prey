import { Routes } from '@angular/router';
import { authGuardFn } from '@auth0/auth0-angular';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./login/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'home',
    loadComponent: () => import('./home/home.page').then((m) => m.HomePage),
    canActivate: [authGuardFn],
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
