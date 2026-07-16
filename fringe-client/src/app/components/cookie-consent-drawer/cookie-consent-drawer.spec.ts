import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection, signal } from '@angular/core';
import { CookieConsentDrawerComponent } from './cookie-consent-drawer';
import { CookieConsentService } from '../../services/cookie-consent.service';
import { getNativeElement } from '../../../testing/native-element';
import { provideRouter } from '@angular/router';

const makeCookieConsentSpy = (
    hasDecided = false,
    accepted = false,
  ): jasmine.SpyObj<CookieConsentService> => {
    const spy = jasmine.createSpyObj<CookieConsentService>(
        'CookieConsentService',
        ['accept', 'reject'],
      ),
      acceptedSignal = signal(accepted),
      hasDecidedSignal = signal(hasDecided);
    Object.defineProperty(spy, 'hasDecided', { get: () => hasDecidedSignal });
    Object.defineProperty(spy, 'accepted', { get: () => acceptedSignal });
    return spy;
  },
  build = async (
    spy: jasmine.SpyObj<CookieConsentService>,
  ): Promise<ComponentFixture<CookieConsentDrawerComponent>> => {
    TestBed.configureTestingModule({
      imports: [CookieConsentDrawerComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: CookieConsentService, useValue: spy },
      ],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(CookieConsentDrawerComponent);
    fixture.detectChanges();
    return fixture;
  };

describe('CookieConsentDrawerComponent', () => {
  it('creates the component', async () => {
    const spy = makeCookieConsentSpy(),
      fixture = await build(spy);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('injects CookieConsentService', async () => {
    const spy = makeCookieConsentSpy();
    await build(spy);
    expect(TestBed.inject(CookieConsentService)).toBe(spy);
  });

  it('renders drawer when hasDecided is false', async () => {
    const spy = makeCookieConsentSpy(false),
      fixture = await build(spy);
    expect(
      getNativeElement(fixture).querySelector('.drawer-card'),
    ).not.toBeNull();
  });

  it('hides drawer when hasDecided is true', async () => {
    const spy = makeCookieConsentSpy(true),
      fixture = await build(spy);
    expect(getNativeElement(fixture).querySelector('.drawer-card')).toBeNull();
  });

  it('calls accept when the accept button is clicked', async () => {
    const spy = makeCookieConsentSpy(false),
      fixture = await build(spy);
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('.btn-accept')
      ?.click();
    expect(spy.accept).toHaveBeenCalled();
  });

  it('calls reject when the reject button is clicked', async () => {
    const spy = makeCookieConsentSpy(false),
      fixture = await build(spy);
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('.btn-reject')
      ?.click();
    expect(spy.reject).toHaveBeenCalled();
  });
});
