import type { Venue } from './models';
import { venueDisplayName } from './venue-display';

const makeVenue = (overrides: Readonly<Partial<Venue>> = {}): Venue => ({
  address: '10330 84 Ave NW',
  name: 'Roxy Theatre',
  phone: '780-555-0101',
  ...overrides,
});

describe('venueDisplayName', () => {
  it('returns an empty string when there is no venue', () => {
    expect(venueDisplayName(undefined)).toBe('');
  });

  it('returns the venue name when it is usable', () => {
    expect(venueDisplayName(makeVenue())).toBe('Roxy Theatre');
  });

  it('falls back to the address when the name is "Unknown"', () => {
    expect(venueDisplayName(makeVenue({ name: 'Unknown' }))).toBe(
      '10330 84 Ave NW',
    );
  });

  it('falls back to the address when the name is "Unknown" in any case', () => {
    expect(venueDisplayName(makeVenue({ name: 'UNKNOWN' }))).toBe(
      '10330 84 Ave NW',
    );
  });

  it('falls back to the address when the name is blank', () => {
    expect(venueDisplayName(makeVenue({ name: '  ' }))).toBe('10330 84 Ave NW');
  });

  it('returns an empty string when neither the name nor the address is usable', () => {
    expect(venueDisplayName(makeVenue({ address: '', name: 'Unknown' }))).toBe(
      '',
    );
  });
});
