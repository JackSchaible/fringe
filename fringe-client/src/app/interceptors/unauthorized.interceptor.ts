import {
  HttpErrorResponse,
  type HttpInterceptorFn,
} from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';
import { inject } from '@angular/core';

const HTTP_UNAUTHORIZED = 401,
  unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
    const auth = inject(AuthService),
      router = inject(Router);

    return next(req).pipe(
      catchError((error: unknown) => {
        if (
          error instanceof HttpErrorResponse &&
          error.status === HTTP_UNAUTHORIZED
        ) {
          auth.clearSession();
          void router.navigate(['/login']);
        }
        return throwError(() => error);
      }),
    );
  };

export { unauthorizedInterceptor };
