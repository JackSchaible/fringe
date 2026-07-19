import type { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { guestGuard } from './guards/guest.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: '/shows' },
  {
    canActivate: [guestGuard],
    loadComponent: async () =>
      import('./pages/login/login').then((mod) => mod.LoginPage),
    path: 'login',
  },
  {
    loadComponent: async () =>
      import('./pages/auth-callback/auth-callback').then(
        (mod) => mod.AuthCallbackPage,
      ),
    path: 'auth/callback',
  },
  {
    canActivate: [authGuard],
    loadComponent: async () =>
      import('./pages/shows/shows').then((mod) => mod.ShowsPage),
    path: 'shows',
  },
  {
    canActivate: [authGuard],
    loadComponent: async () =>
      import('./pages/schedule/schedule').then((mod) => mod.SchedulePage),
    path: 'schedule',
  },
  {
    canActivate: [authGuard],
    loadComponent: async () =>
      import('./pages/group/group').then((mod) => mod.GroupPage),
    path: 'group',
  },
  {
    canActivate: [authGuard],
    loadComponent: async () =>
      import('./pages/availability/availability').then(
        (mod) => mod.AvailabilityPage,
      ),
    path: 'availability',
  },
  {
    loadComponent: async () =>
      import('./pages/privacy/privacy').then((mod) => mod.PrivacyPage),
    path: 'privacy',
  },
  {
    loadComponent: async () =>
      import('./pages/terms/terms').then((mod) => mod.TermsPage),
    path: 'terms',
  },
  { path: '**', redirectTo: '/shows' },
];
