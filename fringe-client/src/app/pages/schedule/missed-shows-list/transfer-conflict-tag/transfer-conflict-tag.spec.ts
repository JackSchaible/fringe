import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { TransferConflict } from '../../../../models';
import { TransferConflictTagComponent } from './transfer-conflict-tag';
import { getNativeElement } from '../../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const namedConflict: TransferConflict = {
    appliedRule: 'matrix',
    availableGapMinutes: 15,
    destinationVenueName: 'Venue Twenty',
    originVenueName: 'Venue Ten',
    requiredGapMinutes: 45,
    travelMode: 'walking',
  },
  build = async (
    conflict: Readonly<TransferConflict>,
  ): Promise<ComponentFixture<TransferConflictTagComponent>> => {
    TestBed.configureTestingModule({
      imports: [TransferConflictTagComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(TransferConflictTagComponent);
    fixture.componentRef.setInput('conflict', conflict);
    fixture.detectChanges();
    return fixture;
  };

describe('TransferConflictTagComponent', () => {
  it('names both venues when both are known', async () => {
    const fixture = await build(namedConflict);
    const tag = getNativeElement(fixture).querySelector('.missed-tag.transfer');
    expect(tag?.textContent).toContain('Venue Ten');
    expect(tag?.textContent).toContain('Venue Twenty');
  });

  it('falls back to a generic message when the origin venue is unknown', async () => {
    const fixture = await build({ ...namedConflict, originVenueName: null });
    const tag = getNativeElement(fixture).querySelector('.missed-tag.transfer');
    expect(tag?.textContent).toContain('next venue');
  });

  it('falls back to a generic message when the destination venue is unknown', async () => {
    const fixture = await build({
      ...namedConflict,
      destinationVenueName: null,
    });
    const tag = getNativeElement(fixture).querySelector('.missed-tag.transfer');
    expect(tag?.textContent).toContain('next venue');
  });
});
