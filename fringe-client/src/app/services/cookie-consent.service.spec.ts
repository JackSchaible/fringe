import { ConsentState, CookieConsentService } from './cookie-consent.service';
import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';

const SINGLE_ELEMENT = 1,
  STORAGE_KEY = 'fringe_cookie_consent',
  buildService = (): CookieConsentService => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection()],
    });
    return TestBed.inject(CookieConsentService);
  },
  testAcceptScenarios = (): void => {
    it('sets consent signal to "accepted"', () => {
      const service = buildService();
      service.accept();
      expect(service.consent()).toBe(ConsentState.accepted);
    });

    it('persists "accepted" to localStorage', () => {
      const service = buildService();
      service.accept();
      expect(localStorage.getItem(STORAGE_KEY)).toBe(ConsentState.accepted);
    });

    it('hasDecided becomes true', () => {
      const service = buildService();
      service.accept();
      expect(service.hasDecided()).toBeTrue();
    });

    it('accepted computed becomes true', () => {
      const service = buildService();
      service.accept();
      expect(service.accepted()).toBeTrue();
    });

    it('injects google-fonts link element into <head>', () => {
      const service = buildService();
      service.accept();
      // Effect runs synchronously in tests with flushEffects / TestBed
      TestBed.tick();
      expect(document.getElementById('google-fonts')).not.toBeNull();
    });

    it('does not duplicate google-fonts link on repeated accept calls', () => {
      const service = buildService();
      service.accept();
      TestBed.tick();
      service.accept();
      TestBed.tick();
      expect(document.querySelectorAll('#google-fonts').length).toBe(
        SINGLE_ELEMENT,
      );
    });
  },
  testInitialStateScenarios = (): void => {
    it('consent is null when nothing in localStorage', () => {
      const service = buildService();
      expect(service.consent()).toBeNull();
    });

    it('hasDecided is false when consent is null', () => {
      const service = buildService();
      expect(service.hasDecided()).toBeFalse();
    });

    it('accepted is false when consent is null', () => {
      const service = buildService();
      expect(service.accepted()).toBeFalse();
    });

    it('reads existing "accepted" value from localStorage', () => {
      localStorage.setItem(STORAGE_KEY, ConsentState.accepted);
      const service = buildService();
      expect(service.consent()).toBe(ConsentState.accepted);
      expect(service.hasDecided()).toBeTrue();
      expect(service.accepted()).toBeTrue();
    });

    it('reads existing "rejected" value from localStorage', () => {
      localStorage.setItem(STORAGE_KEY, ConsentState.rejected);
      const service = buildService();
      expect(service.consent()).toBe(ConsentState.rejected);
      expect(service.hasDecided()).toBeTrue();
      expect(service.accepted()).toBeFalse();
    });
  },
  testRejectScenarios = (): void => {
    it('sets consent signal to "rejected"', () => {
      const service = buildService();
      service.reject();
      expect(service.consent()).toBe(ConsentState.rejected);
    });

    it('persists "rejected" to localStorage', () => {
      const service = buildService();
      service.reject();
      expect(localStorage.getItem(STORAGE_KEY)).toBe(ConsentState.rejected);
    });

    it('hasDecided becomes true', () => {
      const service = buildService();
      service.reject();
      expect(service.hasDecided()).toBeTrue();
    });

    it('accepted stays false', () => {
      const service = buildService();
      service.reject();
      expect(service.accepted()).toBeFalse();
    });

    it('does not inject google-fonts when rejected', () => {
      const service = buildService();
      service.reject();
      TestBed.tick();
      expect(document.getElementById('google-fonts')).toBeNull();
    });
  },
  testTransitionScenarios = (): void => {
    const service = buildService();
    service.accept();
    service.reject();
    expect(service.consent()).toBe(ConsentState.rejected);
    expect(service.accepted()).toBeFalse();
  };

describe('CookieConsentService', () => {
  afterEach(() => {
    localStorage.removeItem(STORAGE_KEY);
    // Remove any google-fonts link injected during tests
    document.getElementById('google-fonts')?.remove();
    document
      .querySelectorAll(
        'link[href*="fonts.googleapis.com"], link[href*="fonts.gstatic.com"]',
      )
      .forEach((el) => {
        el.remove();
      });
  });

  // ── initial state ─────────────────────────────────────────────────────────

  describe('initial state', testInitialStateScenarios);

  // ── accept ────────────────────────────────────────────────────────────────

  describe('accept', testAcceptScenarios);

  // ── reject ────────────────────────────────────────────────────────────────

  describe('reject', testRejectScenarios);

  // ── transition accepted → rejected ────────────────────────────────────────

  it('can change from accepted to rejected', testTransitionScenarios);
});
