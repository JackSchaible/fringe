import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { TravelMode } from '../../../models';
import { TravelModeSelectorComponent } from './travel-mode-selector';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const build = async (
  mode: TravelMode,
): Promise<ComponentFixture<TravelModeSelectorComponent>> => {
  TestBed.configureTestingModule({
    imports: [TravelModeSelectorComponent],
    providers: [provideZonelessChangeDetection()],
  });
  await TestBed.compileComponents();
  const fixture = TestBed.createComponent(TravelModeSelectorComponent);
  fixture.componentRef.setInput('mode', mode);
  fixture.detectChanges();
  return fixture;
};

const ONE_ACTIVE_OPTION = 1;

describe('TravelModeSelectorComponent rendering', () => {
  it('marks the selected mode as active', async () => {
    const fixture = await build('cycling');
    const active = getNativeElement(fixture).querySelector<HTMLElement>(
      '.mode-option.active',
    );
    expect(active?.textContent).toContain('Bicycle or scooter');
  });

  it('renders exactly one active option', async () => {
    const fixture = await build('walking');
    expect(
      getNativeElement(fixture).querySelectorAll('.mode-option.active').length,
    ).toBe(ONE_ACTIVE_OPTION);
  });
});

describe('TravelModeSelectorComponent selection', () => {
  it('emits modeChange when the driving option is clicked', async () => {
    const fixture = await build('walking'),
      emitted: Array<TravelMode> = [];
    fixture.componentInstance.modeChange.subscribe((mode: TravelMode) => {
      emitted.push(mode);
    });
    const drivingButton = Array.from(
      getNativeElement(fixture).querySelectorAll<HTMLElement>('.mode-option'),
    ).find((button) => button.textContent.includes('Driving'));
    drivingButton?.click();
    expect(emitted).toEqual(['driving']);
  });

  it('does not emit modeChange when the active option is clicked again', async () => {
    const fixture = await build('walking'),
      emitted: Array<TravelMode> = [];
    fixture.componentInstance.modeChange.subscribe((mode: TravelMode) => {
      emitted.push(mode);
    });
    getNativeElement(fixture)
      .querySelector<HTMLElement>('.mode-option.active')
      ?.click();
    expect(emitted).toEqual([]);
  });
});
