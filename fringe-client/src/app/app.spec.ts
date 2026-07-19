import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection, signal } from '@angular/core';
import { AppComponent } from './app';
import { AuthService } from './services/auth.service';
import { CookieConsentService } from './services/cookie-consent.service';
import type { User } from './models';
import { getNativeElement } from '../testing/native-element';
import { provideRouter } from '@angular/router';

const build = async (
    authSpy: jasmine.SpyObj<AuthService>,
    consentSpy: jasmine.SpyObj<CookieConsentService>,
  ): Promise<{
    component: AppComponent;
    fixture: ComponentFixture<AppComponent>;
  }> => {
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
        { provide: CookieConsentService, useValue: consentSpy },
      ],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    return { component: fixture.componentInstance, fixture };
  },
  buildProd = async (
    authSpy: jasmine.SpyObj<AuthService>,
    consentSpy: jasmine.SpyObj<CookieConsentService>,
  ): Promise<{
    authSpy: jasmine.SpyObj<AuthService>;
    component: AppComponent;
    fixture: ComponentFixture<AppComponent>;
  }> => {
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
        { provide: CookieConsentService, useValue: consentSpy },
      ],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    return { authSpy, component: fixture.componentInstance, fixture };
  },
  makeAuthSpy = (
    isLoggedIn = false,
    devMode = true,
  ): jasmine.SpyObj<AuthService> => {
    let currentUser: User | null = null;
    if (isLoggedIn) {
      currentUser = {
        displayName: 'Tester',
        email: 'test@test.com',
        groupId: null,
        userId: 'u1',
      };
    }
    const currentUserSignal = signal(currentUser),
      spy = jasmine.createSpyObj<AuthService>('AuthService', [
        'signOut',
        'initDevSession',
        'loadUserFromCognito',
      ]);
    Object.defineProperty(spy, 'currentUser', { get: () => currentUserSignal });
    Object.defineProperty(spy, 'devMode', { get: () => devMode });
    spy.signOut.and.returnValue(Promise.resolve());
    spy.loadUserFromCognito.and.returnValue(Promise.resolve());
    return spy;
  },
  makeCookieConsentSpy = (): jasmine.SpyObj<CookieConsentService> => {
    const acceptedSignal = signal(false),
      hasDecidedSignal = signal(false),
      spy = jasmine.createSpyObj<CookieConsentService>('CookieConsentService', [
        'accept',
        'reject',
      ]);
    Object.defineProperty(spy, 'hasDecided', { get: () => hasDecidedSignal });
    Object.defineProperty(spy, 'accepted', { get: () => acceptedSignal });
    return spy;
  };

describe('AppComponent', () => {
  it('creates the component', async () => {
    const { component } = await build(makeAuthSpy(), makeCookieConsentSpy());
    expect(component).toBeTruthy();
  });

  it('renders the current year in the footer', async () => {
    const expected = new Date().getFullYear(),
      { fixture } = await build(makeAuthSpy(), makeCookieConsentSpy()),
      footerText =
        getNativeElement(fixture).querySelector('.footer-copy')?.textContent ??
        '';
    expect(footerText).toContain(String(expected));
  });

  it('shows nav when user is logged in', async () => {
    const authSpy = makeAuthSpy(true),
      { fixture } = await build(authSpy, makeCookieConsentSpy()),
      nav = getNativeElement(fixture).querySelector('nav');
    expect(nav).not.toBeNull();
  });

  it('hides nav when user is not logged in', async () => {
    const authSpy = makeAuthSpy(false),
      { fixture } = await build(authSpy, makeCookieConsentSpy()),
      nav = getNativeElement(fixture).querySelector('nav');
    expect(nav).toBeNull();
  });

  it('calls authService.signOut when the sign-out button is clicked', async () => {
    const authSpy = makeAuthSpy(true),
      { fixture } = await build(authSpy, makeCookieConsentSpy()),
      button =
        getNativeElement(fixture).querySelector<HTMLButtonElement>(
          '.nav-signout',
        );
    button?.click();
    expect(authSpy.signOut).toHaveBeenCalled();
  });

  it('provides CookieConsentService via the root injector', async () => {
    const consentSpy = makeCookieConsentSpy();
    await build(makeAuthSpy(), consentSpy);
    expect(TestBed.inject(CookieConsentService)).toBe(consentSpy);
  });
});

describe('AppComponent ngOnInit in devMode', () => {
  it('calls initDevSession when devMode is true', async () => {
    const authSpy = makeAuthSpy();
    await build(authSpy, makeCookieConsentSpy());
    expect(authSpy.initDevSession).toHaveBeenCalled();
  });

  it('does NOT call loadUserFromCognito when devMode is true', async () => {
    const authSpy = makeAuthSpy();
    await build(authSpy, makeCookieConsentSpy());
    expect(authSpy.loadUserFromCognito).not.toHaveBeenCalled();
  });
});

describe('AppComponent ngOnInit in prod mode (devMode = false)', () => {
  it('calls loadUserFromCognito when devMode is false', async () => {
    const { authSpy } = await buildProd(
      makeAuthSpy(false, false),
      makeCookieConsentSpy(),
    );
    expect(authSpy.loadUserFromCognito).toHaveBeenCalled();
  });

  it('does NOT call initDevSession when devMode is false', async () => {
    const { authSpy } = await buildProd(
      makeAuthSpy(false, false),
      makeCookieConsentSpy(),
    );
    expect(authSpy.initDevSession).not.toHaveBeenCalled();
  });
});
