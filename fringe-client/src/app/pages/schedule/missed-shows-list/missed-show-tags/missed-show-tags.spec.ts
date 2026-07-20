import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { MissedShowTagsComponent } from './missed-show-tags';
import { getNativeElement } from '../../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const ONE_TAG = 1,
  build = async (
    conflictsWithScheduled: boolean,
    blockedByMembers: ReadonlyArray<string>,
    hasTransferConflict: boolean,
  ): Promise<ComponentFixture<MissedShowTagsComponent>> => {
    TestBed.configureTestingModule({
      imports: [MissedShowTagsComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(MissedShowTagsComponent);
    fixture.componentRef.setInput(
      'conflictsWithScheduled',
      conflictsWithScheduled,
    );
    fixture.componentRef.setInput('blockedByMembers', blockedByMembers);
    fixture.componentRef.setInput('hasTransferConflict', hasTransferConflict);
    fixture.detectChanges();
    return fixture;
  };

describe('MissedShowTagsComponent', () => {
  it('shows a conflict tag when conflictsWithScheduled is true', async () => {
    const fixture = await build(true, [], false);
    expect(
      getNativeElement(fixture).querySelector('.missed-tag.conflict'),
    ).not.toBeNull();
  });

  it('does not show a conflict tag when conflictsWithScheduled is false', async () => {
    const fixture = await build(false, [], false);
    expect(
      getNativeElement(fixture).querySelector('.missed-tag.conflict'),
    ).toBeNull();
  });

  it('shows a blocked tag per blocking member', async () => {
    const fixture = await build(false, ['Bob'], false);
    expect(
      getNativeElement(fixture).querySelectorAll('.missed-tag.blocked').length,
    ).toBe(ONE_TAG);
  });

  it('shows a transfer tag when hasTransferConflict is true', async () => {
    const fixture = await build(false, [], true);
    expect(
      getNativeElement(fixture).querySelector('.missed-tag.transfer'),
    ).not.toBeNull();
  });

  it('does not show a transfer tag when hasTransferConflict is false', async () => {
    const fixture = await build(false, [], false);
    expect(
      getNativeElement(fixture).querySelector('.missed-tag.transfer'),
    ).toBeNull();
  });
});
