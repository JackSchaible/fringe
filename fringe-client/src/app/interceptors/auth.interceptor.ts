import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { from, switchMap } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  return from(auth.getToken()).pipe(
    switchMap(token => {
      if (token) {
        return next(req.clone({ headers: req.headers.set('Authorization', `Bearer ${token}`) }));
      }
      const devId = auth.getDevUserId();
      if (devId) {
        return next(req.clone({ headers: req.headers.set('X-Dev-User-Id', devId) }));
      }
      return next(req);
    }),
  );
};
