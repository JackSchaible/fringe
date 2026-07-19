import type { ComponentFixture } from '@angular/core/testing';

export const getNativeElement = (
  fixture: ComponentFixture<unknown>,
): HTMLElement => {
  const native: unknown = fixture.nativeElement;
  if (!(native instanceof HTMLElement)) {
    throw new Error('fixture.nativeElement is not an HTMLElement');
  }
  return native;
};
