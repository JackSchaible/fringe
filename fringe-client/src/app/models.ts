export interface Show {
  showId: number;
  title: string;
  description?: string;
  plainTextDescription?: string;
  imageUrl?: string;
  tag?: string;
  price: string;
  fee: string;
  lengthInMinutes: number;
  venue?: Venue;
  contentRating?: ContentRating;
  showTimes: string[];
}

export interface Venue {
  name: string;
  address: string;
  phone: string;
}

export interface ContentRating {
  name: string;
  code: string;
  description?: string;
}

export interface Vote {
  showId: number;
  rank: number;
}

export interface Group {
  groupId: string;
  name: string;
  inviteCode: string;
  members: GroupMember[];
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
  groupId?: string;
}

export interface AvailabilityWindow {
  start: string; // ISO 8601 UTC
  end: string;   // ISO 8601 UTC
}

export interface UserAvailability {
  windows: AvailabilityWindow[];
}

export interface AlternateProposal {
  description: string;
  excludedMemberName: string;
  items: ScheduleItem[];
}

export interface ScheduleResponse {
  items: ScheduleItem[];
  alternateProposals: AlternateProposal[];
}
