import type { Venue } from './models';

const UNKNOWN_VENUE_NAME = 'unknown';

const hasUsableName = (venue: Venue): boolean =>
  Boolean(venue.name.trim()) &&
  venue.name.trim().toLowerCase() !== UNKNOWN_VENUE_NAME;

// The scraper falls back to a literal "Unknown" venue name when it can't parse one off the show page, even though it usually still captures the address — prefer the address over showing "Unknown". Returns '' (not undefined) when nothing usable is available, matching this codebase's existing convention for absent optional strings.
export const venueDisplayName = (venue: Venue | undefined): string => {
  if (!venue) {
    return '';
  }
  if (hasUsableName(venue)) {
    return venue.name;
  }
  return venue.address.trim();
};
