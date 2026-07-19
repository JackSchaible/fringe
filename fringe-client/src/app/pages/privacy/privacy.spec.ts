import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { PrivacyPage } from './privacy';
import { getNativeElement } from '../../../testing/native-element';
import { provideRouter } from '@angular/router';
import { provideZonelessChangeDetection } from '@angular/core';

const EMPTY_COUNT = 0,
  build = async (): Promise<ComponentFixture<PrivacyPage>> => {
    TestBed.configureTestingModule({
      imports: [PrivacyPage],
      providers: [provideZonelessChangeDetection(), provideRouter([])],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(PrivacyPage);
    fixture.detectChanges();
    return fixture;
  };

describe('PrivacyPage', () => {
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
