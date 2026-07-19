import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { LoginPage } from './login';
import { provideZonelessChangeDetection } from '@angular/core';

const build = async (
    apiSpy: jasmine.SpyObj<ApiService>,
    authSpy: jasmine.SpyObj<AuthService>,
  ): Promise<{
    component: LoginPage;
    fixture: ComponentFixture<LoginPage>;
    router: Router;
  }> => {
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
        { provide: ApiService, useValue: apiSpy },
      ],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(LoginPage),
      router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    fixture.detectChanges();
    return { component: fixture.componentInstance, fixture, router };
  },
  makeApiSpy = (): jasmine.SpyObj<ApiService> => {
    const spy = jasmine.createSpyObj<ApiService>('ApiService', [
      'verifyCaptcha',
      'upsertMe',
      'updateDisplayName',
    ]);
    spy.verifyCaptcha.and.returnValue(of(undefined));
    spy.upsertMe.and.returnValue(of(undefined));
    spy.updateDisplayName.and.returnValue(of(undefined));
    return spy;
  },
  makeAuthSpy = (): jasmine.SpyObj<AuthService> => {
    const spy = jasmine.createSpyObj<AuthService>('AuthService', [
      'confirmOtp',
      'signInDev',
    ]);
    spy.confirmOtp.and.returnValue(Promise.resolve());
    Object.defineProperty(spy, 'currentUser', { get: () => (): null => null });
    Object.defineProperty(spy, 'devMode', { get: () => true });
    return spy;
  };

describe('LoginPage initial state', () => {
  it('step starts at "email"', async () => {
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    expect(component.step()).toBe('email');
  });

  it('loading starts false', async () => {
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    expect(component.loading()).toBeFalse();
  });

  it('error starts empty', async () => {
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    expect(component.error()).toBe('');
  });

  it('agreedToTerms starts false', async () => {
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    expect(component.agreedToTerms()).toBeFalse();
  });
});

describe('LoginPage sendCode', () => {
  it('advances step to "otp" on success', async () => {
    spyOn(AuthService, 'sendOtp').and.returnValue(Promise.resolve());
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    component.email.set('user@example.com');
    component.captchaToken.set('tok');
    await component.sendCode();
    expect(component.step()).toBe('otp');
  });

  it('sets loading to false after success', async () => {
    spyOn(AuthService, 'sendOtp').and.returnValue(Promise.resolve());
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    component.captchaToken.set('tok');
    await component.sendCode();
    expect(component.loading()).toBeFalse();
  });

  it('sets error message on captcha failure', async () => {
    spyOn(AuthService, 'sendOtp').and.returnValue(Promise.resolve());
    const apiSpy = makeApiSpy();
    apiSpy.verifyCaptcha.and.returnValue(
      throwError(() => new Error('Bad captcha')),
    );
    const { component } = await build(apiSpy, makeAuthSpy());
    component.captchaToken.set('bad');
    await component.sendCode();
    expect(component.error()).toBe('Failed to send code. Please try again.');
    expect(component.step()).toBe('email');
  });

  it('sets error message on sendOtp failure', async () => {
    spyOn(AuthService, 'sendOtp').and.returnValue(
      Promise.reject(new Error('OTP error')),
    );
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    component.captchaToken.set('tok');
    await component.sendCode();
    expect(component.error()).toBe('Failed to send code. Please try again.');
  });

  it('clears error before attempting', async () => {
    spyOn(AuthService, 'sendOtp').and.returnValue(Promise.resolve());
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    component.error.set('old error');
    component.captchaToken.set('tok');
    await component.sendCode();
    expect(component.error()).toBe('');
  });
});

describe('LoginPage verifyCode', () => {
  it('advances step to "username" on success', async () => {
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    component.step.set('otp');
    await component.verifyCode();
    expect(component.step()).toBe('username');
  });

  it('sets error message on confirmOtp failure', async () => {
    const authSpy = makeAuthSpy();
    authSpy.confirmOtp.and.returnValue(Promise.reject(new Error('Wrong code')));
    const { component } = await build(makeApiSpy(), authSpy);
    component.step.set('otp');
    await component.verifyCode();
    expect(component.error()).toBe('Incorrect code. Please try again.');
    expect(component.step()).toBe('otp');
  });

  it('clears error before attempting', async () => {
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    component.step.set('otp');
    component.error.set('stale error');
    await component.verifyCode();
    expect(component.error()).toBe('');
  });

  it('sets loading to false on error path', async () => {
    const authSpy = makeAuthSpy();
    authSpy.confirmOtp.and.returnValue(Promise.reject(new Error('Bad code')));
    const { component } = await build(makeApiSpy(), authSpy);
    component.step.set('otp');
    await component.verifyCode();
    expect(component.loading()).toBeFalse();
  });
});

describe('LoginPage saveUsername', () => {
  it('navigates to /shows after setting a name', async () => {
    const { component, router } = await build(makeApiSpy(), makeAuthSpy());
    component.step.set('username');
    component.username.set('Alice');
    await component.saveUsername();
    expect(router.navigate).toHaveBeenCalledWith(['/shows']);
  });

  it('navigates to /shows even when username is empty (skip)', async () => {
    const { component, router } = await build(makeApiSpy(), makeAuthSpy());
    component.step.set('username');
    component.username.set('');
    await component.saveUsername();
    expect(router.navigate).toHaveBeenCalledWith(['/shows']);
  });

  it('calls updateDisplayName when name is non-empty', async () => {
    const apiSpy = makeApiSpy(),
      { component } = await build(apiSpy, makeAuthSpy());
    component.step.set('username');
    component.username.set('Bob');
    await component.saveUsername();
    expect(apiSpy.updateDisplayName).toHaveBeenCalledWith('Bob');
  });

  it('does not call updateDisplayName for blank username', async () => {
    const apiSpy = makeApiSpy(),
      { component } = await build(apiSpy, makeAuthSpy());
    component.step.set('username');
    component.username.set('   ');
    await component.saveUsername();
    expect(apiSpy.updateDisplayName).not.toHaveBeenCalled();
  });

  it('still navigates when updateDisplayName throws', async () => {
    const apiSpy = makeApiSpy();
    apiSpy.updateDisplayName.and.returnValue(
      throwError(() => new Error('API error')),
    );
    const { component, router } = await build(apiSpy, makeAuthSpy());
    component.step.set('username');
    component.username.set('Carol');
    await component.saveUsername();
    expect(router.navigate).toHaveBeenCalledWith(['/shows']);
  });
});

describe('LoginPage skipUsername', () => {
  it('navigates to /shows', async () => {
    const { component, router } = await build(makeApiSpy(), makeAuthSpy());
    await component.skipUsername();
    expect(router.navigate).toHaveBeenCalledWith(['/shows']);
  });
});

describe('LoginPage back', () => {
  it('resets step to "email"', async () => {
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    component.step.set('otp');
    component.back();
    expect(component.step()).toBe('email');
  });

  it('clears otp signal', async () => {
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    component.otp.set('123456');
    component.back();
    expect(component.otp()).toBe('');
  });

  it('clears error signal', async () => {
    const { component } = await build(makeApiSpy(), makeAuthSpy());
    component.error.set('Some error');
    component.back();
    expect(component.error()).toBe('');
  });
});

describe('LoginPage signInDev', () => {
  it('calls auth.signInDev with trimmed userId', async () => {
    const authSpy = makeAuthSpy(),
      { component } = await build(makeApiSpy(), authSpy);
    component.devUserId.set('  user42  ');
    component.signInDev();
    expect(authSpy.signInDev).toHaveBeenCalledWith('user42');
  });

  it('navigates to /shows', async () => {
    const { component, router } = await build(makeApiSpy(), makeAuthSpy());
    component.devUserId.set('user1');
    component.signInDev();
    expect(router.navigate).toHaveBeenCalledWith(['/shows']);
  });

  it('does nothing when devUserId is empty', async () => {
    const authSpy = makeAuthSpy(),
      { component } = await build(makeApiSpy(), authSpy);
    component.devUserId.set('   ');
    component.signInDev();
    expect(authSpy.signInDev).not.toHaveBeenCalled();
  });
});
