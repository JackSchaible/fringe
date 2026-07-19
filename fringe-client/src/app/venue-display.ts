import type { Venue } from './models';

const UNKNOWN_VENUE_NAME = 'unknown';

const trimmedOrEmpty = (value: string | null): string => (value ?? '').trim();

const hasUsableName = (venue: Venue): boolean => {
  const name = trimmedOrEmpty(venue.name);
  return name !== '' && name.toLowerCase() !== UNKNOWN_VENUE_NAME;
};

// The scraper falls back to a literal "Unknown" venue name when it can't parse one off the show page, even though it usually still captures the address — prefer the address over showing "Unknown". Either field can also be a genuine null when DynamoDB is missing that attribute entirely. Returns '' (not undefined/null) when nothing usable is available, matching this codebase's existing convention for absent optional strings.
export const venueDisplayName = (venue: Venue | undefined): string => {
  if (!venue) {
    return '';
  }
  if (hasUsableName(venue)) {
    return trimmedOrEmpty(venue.name);
  }
  return trimmedOrEmpty(venue.address);
};
