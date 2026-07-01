import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { guestGuard } from './guards/guest.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/shows', pathMatch: 'full' },
  { path: 'login', loadComponent: () => import('./pages/login/login').then(m => m.LoginPage), canActivate: [guestGuard] },
  { path: 'auth/callback', loadComponent: () => import('./pages/auth-callback/auth-callback').then(m => m.AuthCallbackPage) },
  {
    path: 'shows',
    loadComponent: () => import('./pages/shows/shows').then(m => m.ShowsPage),
    canActivate: [authGuard],
  },
  {
    path: 'schedule',
    loadComponent: () => import('./pages/schedule/schedule').then(m => m.SchedulePage),
    canActivate: [authGuard],
  },
  {
    path: 'group',
    loadComponent: () => import('./pages/group/group').then(m => m.GroupPage),
    canActivate: [authGuard],
  },
  { path: '**', redirectTo: '/shows' },
];
