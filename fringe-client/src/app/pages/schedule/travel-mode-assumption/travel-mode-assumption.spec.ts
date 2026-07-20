import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { TravelMode } from '../../../models';
import { TravelModeAssumptionComponent } from './travel-mode-assumption';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const build = async (
  mode: TravelMode,
): Promise<ComponentFixture<TravelModeAssumptionComponent>> => {
  TestBed.configureTestingModule({
    imports: [TravelModeAssumptionComponent],
    providers: [provideZonelessChangeDetection()],
  });
  await TestBed.compileComponents();
  const fixture = TestBed.createComponent(TravelModeAssumptionComponent);
  fixture.componentRef.setInput('mode', mode);
  fixture.detectChanges();
  return fixture;
};

describe('TravelModeAssumptionComponent rendering', () => {
  it('describes walking when walking is selected', async () => {
    const fixture = await build('walking');
    expect(getNativeElement(fixture).textContent).toContain('walking');
  });

  it('describes bicycle or scooter when cycling is selected', async () => {
    const fixture = await build('cycling');
    expect(getNativeElement(fixture).textContent).toContain(
      'bicycle or scooter',
    );
  });

  it('mentions parking when driving is selected', async () => {
    const fixture = await build('driving');
    expect(getNativeElement(fixture).textContent).toContain('parking');
  });
});
