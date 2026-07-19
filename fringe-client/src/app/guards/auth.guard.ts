import { type CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { inject } from '@angular/core';

export const authGuard: CanActivateFn = async () => {
  const authService = inject(AuthService),
    routerService = inject(Router);
  if (await authService.isAuthenticated()) {
    return true;
  }
  return routerService.createUrlTree(['/login']);
};
