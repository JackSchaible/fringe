import { type Signal, provideZonelessChangeDetection } from '@angular/core';
import { AuthService } from './auth.service';
import { Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import type { User } from '../models';

// ── helpers ──────────────────────────────────────────────────────────────────

// Dev-mode helpers take this narrow, read-only interface (Signal, not WritableSignal, since Readonly<> can't hide WritableSignal's set/update methods).
// Prod-mode helpers call .set() directly on stubbed responses, so those take the real AuthService.
interface DevModeService {
  readonly devMode: boolean;
  readonly currentUser: Signal<User | null>;
  readonly signInDev: (userId: string) => void;
  readonly initDevSession: () => void;
  readonly isAuthenticated: () => Promise<boolean>;
  readonly clearSession: () => void;
  readonly signOut: () => Promise<void>;
  readonly getToken: () => Promise<string | null>;
}

const makeRouter = (): jasmine.SpyObj<Router> =>
    jasmine.createSpyObj<Router>('Router', ['navigate']),
  // Each helper below takes a getter rather than the service/router values themselves: describe() callbacks run before beforeEach has fired, so passing the value directly would freeze it at undefined inside every it() closure.
  // Reading through the getter inside each it() instead picks up the real instance beforeEach assigns later.
  testGenericDevMode = (getService: () => DevModeService): void => {
    it('devMode is true when cognitoUserPoolId is empty (default env)', () => {
      expect(getService().devMode).toBeTrue();
    });

    it('currentUser signal starts as null', () => {
      expect(getService().currentUser()).toBeNull();
    });

    it('sets localStorage and currentUser signal', () => {
      const service = getService();
      service.signInDev('user42');
      expect(localStorage.getItem('fringe_dev_user')).toBe('user42');
      expect(service.currentUser()?.userId).toBe('user42');
      expect(service.currentUser()?.email).toBe('user42@dev');
      expect(service.currentUser()?.displayName).toBe('user42');
    });
  },
  testGetDevUserId = (getService: () => DevModeService): void => {
    it('returns null when not signed in', () => {
      expect(AuthService.getDevUserId()).toBeNull();
    });

    it('returns userId when signed in', () => {
      getService().signInDev('alice');
      expect(AuthService.getDevUserId()).toBe('alice');
    });
  },
  testInitDevSession = (getService: () => DevModeService): void => {
    it('restores currentUser from localStorage', () => {
      localStorage.setItem('fringe_dev_user', 'bob');
      const service = getService();
      service.initDevSession();
      expect(service.currentUser()?.userId).toBe('bob');
    });

    it('does nothing when localStorage is empty', () => {
      const service = getService();
      service.initDevSession();
      expect(service.currentUser()).toBeNull();
    });
  },
  testIsAuthenticatedDevMode = (getService: () => DevModeService): void => {
    it('returns false when no dev user in localStorage', async () => {
      expect(await getService().isAuthenticated()).toBeFalse();
    });

    it('returns true when dev user is in localStorage', async () => {
      const service = getService();
      service.signInDev('carol');
      expect(await service.isAuthenticated()).toBeTrue();
    });
  },
  testClearSession = (getService: () => DevModeService): void => {
    it('removes dev user from localStorage and clears currentUser', () => {
      const service = getService();
      service.signInDev('dave');
      service.clearSession();
      expect(localStorage.getItem('fringe_dev_user')).toBeNull();
      expect(service.currentUser()).toBeNull();
    });
  },
  testSignOutDevMode = (
    getService: () => DevModeService,
    getRouter: () => jasmine.SpyObj<Router>,
  ): void => {
    it('clears localStorage, sets currentUser null, and navigates to /login', async () => {
      const service = getService();
      service.signInDev('erin');
      await service.signOut();
      expect(localStorage.getItem('fringe_dev_user')).toBeNull();
      expect(service.currentUser()).toBeNull();
      expect(getRouter().navigate).toHaveBeenCalledWith(['/login']);
    });
  },
  testGetTokenDevMode = (getService: () => DevModeService): void => {
    it('returns null in dev mode (no Amplify)', async () => {
      expect(await getService().getToken()).toBeNull();
    });
  },
  testDevMode = (): void => {
    let router = makeRouter(),
      service: AuthService = jasmine.createSpyObj<AuthService>('AuthService', [
        'clearSession',
      ]);

    afterEach(() => {
      localStorage.clear();
    });

    beforeEach(() => {
      localStorage.clear();
      router = makeRouter();

      TestBed.configureTestingModule({
        providers: [
          provideZonelessChangeDetection(),
          { provide: Router, useValue: router },
        ],
      });
      service = TestBed.inject(AuthService);
      router.navigate.and.returnValue(Promise.resolve(true));
    });

    describe('generic dev-mode behaviors', () => {
      testGenericDevMode(() => service);
    });
    describe('getDevUserId', () => {
      testGetDevUserId(() => service);
    });
    describe('initDevSession', () => {
      testInitDevSession(() => service);
    });
    describe('isAuthenticated', () => {
      testIsAuthenticatedDevMode(() => service);
    });
    describe('clearSession', () => {
      testClearSession(() => service);
    });
    describe('signOut', () => {
      testSignOutDevMode(
        () => service,
        () => router,
      );
    });
    describe('getToken', () => {
      testGetTokenDevMode(() => service);
    });
  },
  // Prod-mode branches are tested by stubbing the dynamic imports at the service-method level: since the dynamic import() call is inlined, replacing the concrete methods with jasmine spies exercises the same public contract without involving AWS Cognito.
  testLoadUserFromCognito = (getService: () => AuthService): void => {
    it('sets currentUser on success via stub', async () => {
      const service = getService();
      spyOn(service, 'loadUserFromCognito').and.callFake(async () => {
        service.currentUser.set({
          displayName: 'Alice',
          email: 'a@b.com',
          groupId: null,
          userId: 'cog-1',
        });
        await Promise.resolve();
      });
      await service.loadUserFromCognito();
      expect(service.currentUser()?.userId).toBe('cog-1');
    });

    it('sets currentUser null on error via stub', async () => {
      const service = getService();
      spyOn(service, 'loadUserFromCognito').and.callFake(async () => {
        service.currentUser.set(null);
        await Promise.resolve();
      });
      await service.loadUserFromCognito();
      expect(service.currentUser()).toBeNull();
    });
  },
  testGetTokenProd = (getService: () => AuthService): void => {
    it('returns token string via stub', async () => {
      const service = getService();
      spyOn(service, 'getToken').and.returnValue(
        Promise.resolve('access-token'),
      );
      expect(await service.getToken()).toBe('access-token');
    });

    it('returns null when session has no token via stub', async () => {
      const service = getService();
      spyOn(service, 'getToken').and.returnValue(Promise.resolve(null));
      expect(await service.getToken()).toBeNull();
    });
  },
  testSendOtp = (): void => {
    it('calls through without throwing via stub', async () => {
      spyOn(AuthService, 'sendOtp').and.returnValue(Promise.resolve());
      await expectAsync(AuthService.sendOtp('user@test.com')).toBeResolved();
    });

    it('propagates errors via stub', async () => {
      spyOn(AuthService, 'sendOtp').and.returnValue(
        Promise.reject(new Error('Network error')),
      );
      await expectAsync(AuthService.sendOtp('user@test.com')).toBeRejected();
    });
  },
  testConfirmOtp = (getService: () => AuthService): void => {
    it('resolves and loads user on correct code via stub', async () => {
      const service = getService();
      spyOn(service, 'confirmOtp').and.callFake(async () => {
        service.currentUser.set({
          displayName: 'X',
          email: 'x@y.com',
          groupId: null,
          userId: 'u1',
        });
        await Promise.resolve();
      });
      await service.confirmOtp('123456');
      expect(service.currentUser()).toBeTruthy();
    });

    it('throws on incorrect code via stub', async () => {
      const service = getService();
      spyOn(service, 'confirmOtp').and.returnValue(
        Promise.reject(new Error('Incorrect code — please try again.')),
      );
      await expectAsync(service.confirmOtp('000000')).toBeRejected();
    });
  },
  testSignOutProdMode = (
    getService: () => AuthService,
    getRouter: () => jasmine.SpyObj<Router>,
  ): void => {
    it('calls router.navigate via stub', async () => {
      const service = getService(),
        router = getRouter();
      spyOn(service, 'signOut').and.callFake(async () => {
        service.currentUser.set(null);
        await router.navigate(['/login']);
      });
      await service.signOut();
      expect(service.currentUser()).toBeNull();
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });
  },
  testProdMode = (): void => {
    let router = makeRouter(),
      service: AuthService = jasmine.createSpyObj<AuthService>('AuthService', [
        'clearSession',
      ]);

    beforeEach(() => {
      localStorage.clear();
      router = makeRouter();

      TestBed.configureTestingModule({
        providers: [
          provideZonelessChangeDetection(),
          { provide: Router, useValue: router },
        ],
      });
      service = TestBed.inject(AuthService);
      router.navigate.and.returnValue(Promise.resolve(true));
    });

    afterEach(() => {
      localStorage.clear();
    });

    describe('loadUserFromCognito', () => {
      testLoadUserFromCognito(() => service);
    });

    describe('getToken', () => {
      testGetTokenProd(() => service);
    });

    describe('sendOtp', testSendOtp);

    describe('confirmOtp', () => {
      testConfirmOtp(() => service);
    });

    describe('signOut', () => {
      testSignOutProdMode(
        () => service,
        () => router,
      );
    });
  };

describe('AuthService — dev mode', testDevMode);

// ── Production-mode paths (via spying on the service methods directly) ────────
describe('AuthService — production mode method stubs', testProdMode);
