export interface Show {
  readonly showId: number;
  readonly title: string;
  readonly description?: string;
  readonly plainTextDescription?: string;
  readonly imageUrl?: string;
  readonly tag?: string;
  readonly price: string;
  readonly fee: string;
  readonly lengthInMinutes: number;
  readonly venue?: Venue;
  readonly contentRating?: ContentRating;
  readonly showTimes: ReadonlyArray<string>;
}

export interface Venue {
  // The scraper doesn't always capture every field (e.g. an address without a parsed name, or vice versa) — DynamoDB has no schema to enforce these as required, so a missing attribute deserializes to a genuine null here.
  readonly name: string | null;
  readonly address: string | null;
  readonly phone: string | null;
}

export interface ContentRating {
  readonly name: string;
  readonly code: string;
  readonly description?: string;
}

export interface Vote {
  showId: number;
  rank: number;
}

export interface Group {
  groupId: string;
  name: string;
  inviteCode: string;
  members: Array<GroupMember>;
}

export interface GroupMember {
  userId: string;
  displayName?: string;
  email?: string;
  voteCount: number;
}

export interface ScheduleItem {
  show: Show;
  showTime: string;
  groupScore: number;
}

export interface User {
  userId: string;
  email: string;
  displayName: string;
  groupId: string | null;
}

export interface AvailabilityWindow {
  // Both are in ISO 8601 UTC
  start: string;
  end: string;
}

export interface UserAvailability {
  windows: Array<AvailabilityWindow>;
}

export interface AlternateProposal {
  description: string;
  excludedMemberName: string;
  items: Array<ScheduleItem>;
}

export interface MissedShow {
  show: Show;
  conflictsWithScheduled: boolean;
  blockedByMembers: Array<string>;
}

export type TravelMode = 'walking' | 'cycling' | 'driving';

export const TRAVEL_MODES: ReadonlyArray<TravelMode> = [
  'walking',
  'cycling',
  'driving',
];

export const isTravelMode = (value: unknown): value is TravelMode =>
  typeof value === 'string' &&
  (TRAVEL_MODES as ReadonlyArray<string>).includes(value);

export interface ScheduleResponse {
  items: Array<ScheduleItem>;
  alternateProposals: Array<AlternateProposal>;
  missedShows: Array<MissedShow>;
  hasVotes: boolean;
  travelMode: TravelMode;
}
