import { Router, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { TestBed } from '@angular/core/testing';
import { guestGuard } from './guest.guard';
import { provideZonelessChangeDetection } from '@angular/core';

describe('guestGuard', () => {
  let authService: jasmine.SpyObj<AuthService> =
      jasmine.createSpyObj<AuthService>('AuthService', ['isAuthenticated']),
    router: jasmine.SpyObj<Router> = jasmine.createSpyObj<Router>('Router', [
      'createUrlTree',
      'navigate',
    ]);

  beforeEach(() => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', [
      'isAuthenticated',
    ]);
    router = jasmine.createSpyObj<Router>('Router', [
      'createUrlTree',
      'navigate',
    ]);
    router.createUrlTree.and.callFake(() => new UrlTree());

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
      ],
    });
  });

  const runGuard = async (): Promise<Awaited<ReturnType<typeof guestGuard>>> =>
    // ActivatedRouteSnapshot/RouterStateSnapshot have no public constructor
    // (their base Tree/TreeNode classes aren't exported), and guestGuard
    // Ignores both params, so a real instance isn't obtainable for this fake.
    TestBed.runInInjectionContext(async () =>
      // eslint-disable-next-line @typescript-eslint/no-unsafe-type-assertion
      guestGuard({} as never, {} as never),
    );

  it('returns true (allow) when user is NOT authenticated', async () => {
    authService.isAuthenticated.and.returnValue(Promise.resolve(false));
    const result = await runGuard();
    expect(result).toBeTrue();
  });

  it('redirects to /shows when user IS authenticated', async () => {
    authService.isAuthenticated.and.returnValue(Promise.resolve(true));
    const result = await runGuard();
    expect(router.createUrlTree).toHaveBeenCalledWith(['/shows']);
    expect(result).not.toBeTrue();
    expect(result instanceof Object).toBeTrue();
  });
});
