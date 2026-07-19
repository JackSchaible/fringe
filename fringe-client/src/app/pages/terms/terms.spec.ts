import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { TermsPage } from './terms';
import { getNativeElement } from '../../../testing/native-element';
import { provideRouter } from '@angular/router';
import { provideZonelessChangeDetection } from '@angular/core';

const EMPTY_COUNT = 0,
  build = async (): Promise<ComponentFixture<TermsPage>> => {
    TestBed.configureTestingModule({
      imports: [TermsPage],
      providers: [provideZonelessChangeDetection(), provideRouter([])],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(TermsPage);
    fixture.detectChanges();
    return fixture;
  };

describe('TermsPage', () => {
  it('creates the component', async () => {
    const fixture = await build();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders some text content', async () => {
    const fixture = await build();
    expect(getNativeElement(fixture).textContent.length).toBeGreaterThan(
      EMPTY_COUNT,
    );
  });
});
