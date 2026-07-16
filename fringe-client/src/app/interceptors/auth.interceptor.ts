import { from, switchMap } from 'rxjs';
import { AuthService } from '../services/auth.service';
import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  return from(auth.getToken()).pipe(
    switchMap((token) => {
      if (token !== null) {
        return next(
          req.clone({
            headers: req.headers.set('Authorization', `Bearer ${token}`),
          }),
        );
      }
      const devId = AuthService.getDevUserId();
      if (devId !== null) {
        return next(
          req.clone({ headers: req.headers.set('X-Dev-User-Id', devId) }),
        );
      }
      return next(req);
    }),
  );
};
