import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { AuthService } from '../services/auth.service';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { provideZonelessChangeDetection } from '@angular/core';

/**
 * Helper: dispatches one micro-task tick so the async `getToken()` Promise
 * inside the interceptor resolves before we call httpMock.expectOne().
 */
const NEXT_TICK_MS = 0,
  flushPromises = async (): Promise<void> =>
    new Promise((resolve) => {
      setTimeout(resolve, NEXT_TICK_MS);
    }),
  setup = (token: string | null, devUserId: string | null): HttpClient => {
    const authService = jasmine.createSpyObj<AuthService>('AuthService', [
      'getToken',
    ]);
    authService.getToken.and.returnValue(Promise.resolve(token));
    spyOn(AuthService, 'getDevUserId').and.returnValue(devUserId);

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        { provide: AuthService, useValue: authService },
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    return TestBed.inject(HttpClient);
  };

describe('authInterceptor', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('adds Authorization header when token is present', async () => {
    setup('my-bearer-token', null).get('/api/test').subscribe();
    await flushPromises();
    const req = TestBed.inject(HttpTestingController).expectOne('/api/test');
    expect(req.request.headers.get('Authorization')).toBe(
      'Bearer my-bearer-token',
    );
    req.flush({});
  });

  it('adds X-Dev-User-Id header when no token but devUserId present', async () => {
    setup(null, 'user42').get('/api/test').subscribe();
    await flushPromises();
    const req = TestBed.inject(HttpTestingController).expectOne('/api/test');
    expect(req.request.headers.get('X-Dev-User-Id')).toBe('user42');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('sends request without extra headers when no token and no devUserId', async () => {
    setup(null, null).get('/api/test').subscribe();
    await flushPromises();
    const req = TestBed.inject(HttpTestingController).expectOne('/api/test');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    expect(req.request.headers.has('X-Dev-User-Id')).toBeFalse();
    req.flush({});
  });

  it('prefers Authorization over X-Dev-User-Id when both are available', async () => {
    setup('bearer-token', 'user42').get('/api/test').subscribe();
    await flushPromises();
    const req = TestBed.inject(HttpTestingController).expectOne('/api/test');
    expect(req.request.headers.get('Authorization')).toBe(
      'Bearer bearer-token',
    );
    expect(req.request.headers.has('X-Dev-User-Id')).toBeFalse();
    req.flush({});
  });
});
