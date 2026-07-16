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
  readonly name: string;
  readonly address: string;
  readonly phone: string;
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

export interface ScheduleResponse {
  items: Array<ScheduleItem>;
  alternateProposals: Array<AlternateProposal>;
  missedShows: Array<MissedShow>;
  hasVotes: boolean;
}
