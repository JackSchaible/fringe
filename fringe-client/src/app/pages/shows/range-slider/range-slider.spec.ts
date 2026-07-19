import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { RangeSliderComponent } from './range-slider';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const RANGE_MIN = 0,
  RANGE_MAX = 100,
  NARROWED_MIN = 20,
  NARROWED_MAX = 80,
  build = async (
    value: readonly [number, number] = [RANGE_MIN, RANGE_MAX],
  ): Promise<ComponentFixture<RangeSliderComponent>> => {
    TestBed.configureTestingModule({
      imports: [RangeSliderComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(RangeSliderComponent);
    fixture.componentRef.setInput('min', RANGE_MIN);
    fixture.componentRef.setInput('max', RANGE_MAX);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    return fixture;
  };

describe('RangeSliderComponent rendering', () => {
  it('renders the current min and max values', async () => {
    const fixture = await build([NARROWED_MIN, NARROWED_MAX]);
    expect(
      getNativeElement(fixture).querySelector('.range-slider-values')
        ?.textContent,
    ).toContain(`${NARROWED_MIN}`);
    expect(
      getNativeElement(fixture).querySelector('.range-slider-values')
        ?.textContent,
    ).toContain(`${NARROWED_MAX}`);
  });

  it('applies the label when provided', async () => {
    const fixture = await build();
    fixture.componentRef.setInput('label', 'Price');
    fixture.detectChanges();
    expect(
      getNativeElement(fixture).querySelector('.range-slider-label')
        ?.textContent,
    ).toContain('Price');
  });
});

describe('RangeSliderComponent input events', () => {
  it('emits an updated min clamped below the current max', async () => {
    const fixture = await build([RANGE_MIN, NARROWED_MAX]),
      emitted: Array<readonly [number, number]> = [];
    fixture.componentInstance.valueChange.subscribe((value) => {
      emitted.push(value);
    });
    const [minInput] = getNativeElement(
      fixture,
    ).querySelectorAll<HTMLInputElement>('.range-slider-input');
    minInput.value = `${NARROWED_MIN}`;
    minInput.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([[NARROWED_MIN, NARROWED_MAX]]);
  });

  it('clamps a min drag so it cannot exceed the current max', async () => {
    const fixture = await build([RANGE_MIN, NARROWED_MIN]),
      emitted: Array<readonly [number, number]> = [];
    fixture.componentInstance.valueChange.subscribe((value) => {
      emitted.push(value);
    });
    const [minInput] = getNativeElement(
      fixture,
    ).querySelectorAll<HTMLInputElement>('.range-slider-input');
    minInput.value = `${NARROWED_MAX}`;
    minInput.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([[NARROWED_MIN, NARROWED_MIN]]);
  });

  it('emits an updated max clamped above the current min', async () => {
    const fixture = await build([NARROWED_MIN, RANGE_MAX]),
      emitted: Array<readonly [number, number]> = [];
    fixture.componentInstance.valueChange.subscribe((value) => {
      emitted.push(value);
    });
    const [, maxInput] = getNativeElement(
      fixture,
    ).querySelectorAll<HTMLInputElement>('.range-slider-input');
    maxInput.value = `${NARROWED_MAX}`;
    maxInput.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([[NARROWED_MIN, NARROWED_MAX]]);
  });
});
