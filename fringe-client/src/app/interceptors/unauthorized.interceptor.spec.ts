import {
  HttpClient,
  type HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { unauthorizedInterceptor } from './unauthorized.interceptor';

const HTTP_INTERNAL_SERVER_ERROR = 500,
  setup = (): {
    authService: jasmine.SpyObj<AuthService>;
    router: jasmine.SpyObj<Router>;
  } => {
    const authService = jasmine.createSpyObj<AuthService>('AuthService', [
        'clearSession',
      ]),
      router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
        provideHttpClient(withInterceptors([unauthorizedInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    return { authService, router };
  };

describe('unauthorizedInterceptor', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('passes through successful responses unchanged', (done) => {
    const { authService } = setup();
    TestBed.inject(HttpClient)
      .get('/api/test')
      .subscribe({
        error: () => {
          fail('should not error');
        },
        next: () => {
          done();
        },
      });
    TestBed.inject(HttpTestingController)
      .expectOne('/api/test')
      .flush({ ok: true });
    expect(authService.clearSession).not.toHaveBeenCalled();
  });

  it('calls clearSession and navigates to /login on 401', (done) => {
    const { authService, router } = setup();
    TestBed.inject(HttpClient)
      .get('/api/test')
      .subscribe({
        error: () => {
          expect(authService.clearSession).toHaveBeenCalled();
          expect(router.navigate).toHaveBeenCalledWith(['/login']);
          done();
        },
      });
    TestBed.inject(HttpTestingController)
      .expectOne('/api/test')
      .flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
  });
});

describe('unauthorizedInterceptor non-401 errors', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('does NOT call clearSession on non-401 errors', (done) => {
    const { authService, router } = setup();
    TestBed.inject(HttpClient)
      .get('/api/test')
      .subscribe({
        error: () => {
          expect(authService.clearSession).not.toHaveBeenCalled();
          expect(router.navigate).not.toHaveBeenCalled();
          done();
        },
      });
    TestBed.inject(HttpTestingController)
      .expectOne('/api/test')
      .flush('Forbidden', { status: 403, statusText: 'Forbidden' });
  });

  it('propagates 500 errors without clearing session', (done) => {
    const { authService } = setup();
    TestBed.inject(HttpClient)
      .get('/api/test')
      .subscribe({
        error: (err: HttpErrorResponse) => {
          expect(err.status).toBe(HTTP_INTERNAL_SERVER_ERROR);
          expect(authService.clearSession).not.toHaveBeenCalled();
          done();
        },
      });
    TestBed.inject(HttpTestingController)
      .expectOne('/api/test')
      .flush('Server Error', {
        status: 500,
        statusText: 'Internal Server Error',
      });
  });
});

describe('unauthorizedInterceptor error propagation', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('still propagates the error to the subscriber after handling 401', (done) => {
    let receivedError = false;
    setup();
    TestBed.inject(HttpClient)
      .get('/api/test')
      .subscribe({
        error: () => {
          receivedError = true;
          done();
        },
      });
    TestBed.inject(HttpTestingController)
      .expectOne('/api/test')
      .flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
    expect(receivedError).toBeTrue();
  });
});
