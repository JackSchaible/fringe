import { Router, provideRouter } from '@angular/router';
import { AuthCallbackPage } from './auth-callback';
import { TestBed } from '@angular/core/testing';
import { getNativeElement } from '../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

describe('AuthCallbackPage', () => {
  let component: AuthCallbackPage | null = null,
    navigateSpy: jasmine.Spy | null = null;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [AuthCallbackPage],
      providers: [provideZonelessChangeDetection(), provideRouter([])],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(AuthCallbackPage),
      router = TestBed.inject(Router);
    component = fixture.componentInstance;
    navigateSpy = spyOn(router, 'navigate').and.returnValue(
      Promise.resolve(true),
    );
  });

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('navigates to /shows on init', async () => {
    const fixture = TestBed.createComponent(AuthCallbackPage);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(navigateSpy).toHaveBeenCalledWith(['/shows']);
  });

  it('renders the redirecting message', () => {
    const fixture = TestBed.createComponent(AuthCallbackPage);
    fixture.detectChanges();
    expect(getNativeElement(fixture).textContent).toContain('Redirecting');
  });
});
