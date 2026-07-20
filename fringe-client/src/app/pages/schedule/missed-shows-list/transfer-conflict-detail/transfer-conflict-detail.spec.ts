import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { TransferConflict } from '../../../../models';
import { TransferConflictDetailComponent } from './transfer-conflict-detail';
import { getNativeElement } from '../../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const conflict: TransferConflict = {
    appliedRule: 'matrix',
    availableGapMinutes: 15,
    destinationShowTitle: 'Stand-Up Spectacular',
    destinationVenueName: 'Festival Hub Main Stage',
    originShowTitle: 'The Comedy Hour',
    originVenueName: 'Roxy Theatre',
    requiredGapMinutes: 45,
    travelMode: 'walking',
  },
  build = async (
    input: Readonly<TransferConflict>,
  ): Promise<ComponentFixture<TransferConflictDetailComponent>> => {
    TestBed.configureTestingModule({
      imports: [TransferConflictDetailComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(TransferConflictDetailComponent);
    fixture.componentRef.setInput('conflict', input);
    fixture.detectChanges();
    return fixture;
  };

describe('TransferConflictDetailComponent', () => {
  it('names both shows and venues when venue names are known', async () => {
    const fixture = await build(conflict);
    const detail = getNativeElement(fixture).querySelector('.missed-detail');
    expect(detail?.textContent).toContain('The Comedy Hour');
    expect(detail?.textContent).toContain('Roxy Theatre');
    expect(detail?.textContent).toContain('Stand-Up Spectacular');
    expect(detail?.textContent).toContain('Festival Hub Main Stage');
  });

  it('shows only the show title when the origin venue name is unknown', async () => {
    const fixture = await build({ ...conflict, originVenueName: null });
    const detail = getNativeElement(fixture).querySelector('.missed-detail');
    expect(detail?.textContent).toContain('The Comedy Hour');
    expect(detail?.textContent).not.toContain('Roxy Theatre');
  });

  it('shows only the show title when the destination venue name is unknown', async () => {
    const fixture = await build({ ...conflict, destinationVenueName: null });
    const detail = getNativeElement(fixture).querySelector('.missed-detail');
    expect(detail?.textContent).toContain('Stand-Up Spectacular');
    expect(detail?.textContent).not.toContain('Festival Hub Main Stage');
  });

  it('shows the available and required gap in minutes', async () => {
    const fixture = await build(conflict);
    const detail = getNativeElement(fixture).querySelector('.missed-detail');
    expect(detail?.textContent).toContain('15');
    expect(detail?.textContent).toContain('45');
  });

  it('shows a hint suggesting the user try a different travel mode', async () => {
    const fixture = await build(conflict);
    const detail = getNativeElement(fixture).querySelector('.missed-detail');
    expect(detail?.textContent).toContain('travel mode');
  });
});
