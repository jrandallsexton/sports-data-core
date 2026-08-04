import { apiClient } from './client';

// Matches SportsData.Api.Application.Common.Enums.PickType (by name).
export type PickType = 'StraightUp' | 'AgainstTheSpread' | 'OverUnder';

// Matches SportsData.Api.Application.Common.Enums.TiebreakerType.
export type TiebreakerType = 'TotalPoints' | 'HomeAndAwayScores' | 'EarliestSubmission';

// Matches SportsData.Api.Application.Common.Enums.TiebreakerTiePolicy.
// Only one value today; kept as a union so adding more is a no-code-change lift.
export type TiebreakerTiePolicy = 'EarliestSubmission';

/** Matches SportsData.Api.Application.Common.Enums.JoinPolicy (by name).
 *  Commissioner INPUT; the read-side truth for "can I still join?" is
 *  closesAtUtc / isJoinable on the DTO. */
export type JoinPolicy = 'Open' | 'CloseAtFirstGame';

// NCAA ranking-filter enum names accepted by the BE (null = no filter).
export type NcaaRankingFilter =
  | 'AP_TOP_5'
  | 'AP_TOP_10'
  | 'AP_TOP_15'
  | 'AP_TOP_20'
  | 'AP_TOP_25';

// Fields common to every sport's create-league request. Matches `buildShared`
// in sd-ui/src/api/leagues/requests/createLeagueRequests.js.
interface CreateLeagueRequestBase {
  name: string;
  description: string | null;
  pickType: PickType;
  tiebreakerType: TiebreakerType;
  tiebreakerTiePolicy: TiebreakerTiePolicy;
  useConfidencePoints: boolean;
  isPublic: boolean;
  /** Open (default) or CloseAtFirstGame. Absent ⇒ Open server-side; the mobile
   *  create form always sends it. */
  joinPolicy: JoinPolicy;
  dropLowWeeksCount: number;
  startsOn: string | null;
  endsOn: string | null;
}

export interface CreateFootballNcaaLeagueRequest extends CreateLeagueRequestBase {
  rankingFilter: NcaaRankingFilter | null;
  conferenceSlugs: string[];
}

export interface CreateFootballNflLeagueRequest extends CreateLeagueRequestBase {
  divisionSlugs: string[];
}

export interface CreateBaseballMlbLeagueRequest extends CreateLeagueRequestBase {
  divisionSlugs: string[];
}

export interface CreateLeagueResponse {
  id: string;
}

export interface LeagueMember {
  userId: string;
  username: string;
  role: string;
}

/**
 * React Query key factory for league data — colocated with the API module so
 * screens and shared components import it from one place (route modules must
 * not be the home of shared cache keys).
 */
export const leaguesKeys = {
  mine: ['leagues', 'mine'] as const,
  detail: (id: string) => ['league', id] as const,
  public: ['leagues', 'public'] as const,
  invitations: ['leagues', 'invitations'] as const,
};

/**
 * A pending league invitation awaiting the user's answer. Matches the BE
 * PendingInvitationDto — powers the "Pending Invitations" home card. Embeds
 * the league's full PublicLeague shape so JoinLeagueConfirmSheet renders
 * identical details to public-league discovery.
 */
export interface PendingInvitation {
  invitationId: string;
  /** Display name of the inviting member. */
  invitedBy: string;
  invitedUtc: string;
  league: PublicLeague;
}

/** A registered user invitable to a league (from invite search). No email. */
export interface InviteableUser {
  userId: string;
  username: string;
  displayName: string;
}

// Subset of the BE LeagueDetailDto used by the invite preview and the
// expandable league overview on My Leagues. Mirrors what sd-ui's LeagueDetail
// page renders, minus its Danger Zone (mobile has no delete affordance).
export interface LeagueDetail {
  id: string;
  name: string;
  description: string | null;
  pickType: PickType;
  useConfidencePoints: boolean;
  tiebreakerType: TiebreakerType;
  tiebreakerTiePolicy: TiebreakerTiePolicy;
  /** NCAA-only AP-poll filter; null for every other sport. */
  rankingFilter: NcaaRankingFilter | null;
  /** Conferences (NCAA) or divisions (NFL/MLB) — see the BE's naming note. */
  conferenceSlugs: string[];
  isPublic: boolean;
  /** League window. Null on either side = open-ended; both null = full season. */
  startsOn: string | null;
  endsOn: string | null;
  /** Non-null once the league's season has passed — read-only everywhere. */
  deactivatedUtc?: string | null;
  /**
   * Roster size. Always populated, including for non-members — unlike
   * `members`, which the BE withholds from non-members. Render counts from
   * this, never from `members.length`.
   */
  memberCount: number;
  /** True when the caller belongs to this league. Gates the roster below. */
  isMember: boolean;
  /** The roster. Empty for non-members (invite preview, public browse). */
  members: LeagueMember[];
  /** Join policy the commissioner chose; render `closesAtUtc`/`isJoinable`
   *  for the actual state, not this. */
  joinPolicy: JoinPolicy;
  /** Computed instant joining closes; null = open with no fixed deadline. */
  closesAtUtc: string | null;
  /** False once closed/deactivated — gates the Join affordance. */
  isJoinable: boolean;
}

/**
 * A public league the caller can browse and (maybe) join. Mirrors the BE
 * PublicLeagueDto. Sport/League/PickType are the BE ENUM values (Sport/League
 * as names, PickType as its int); render helpers map them to labels.
 */
export interface PublicLeague {
  id: string;
  name: string;
  description: string;
  commissioner: string;
  rankingFilter: number;
  /** PickType int: 1=StraightUp, 2=AgainstTheSpread, 3=OverUnder. */
  pickType: number;
  useConfidencePoints: boolean;
  dropLowWeeksCount: number;
  sport: 'FootballNcaa' | 'FootballNfl' | 'BaseballMlb';
  league: 'NCAAF' | 'NFL' | 'MLB' | 'NBA';
  seasonYear: number;
  memberCount: number;
  startsOn: string | null;
  endsOn: string | null;
  tiebreakerType: TiebreakerType;
  tiebreakerTiePolicy: TiebreakerTiePolicy;
  joinPolicy: JoinPolicy;
  /** When joining closes; null = open with no fixed deadline. */
  closesAtUtc: string | null;
  /** False once closed — badge/hide rather than offering a Join that 400s. */
  isJoinable: boolean;
}

// Matches SportsData.Api.Application.UI.Leagues.Dtos.LeagueSummaryDto.
export interface LeagueSummary {
  id: string;
  name: string;
  sport: 'FootballNcaa' | 'FootballNfl' | 'BaseballMlb';
  /** Sport-league the group plays. */
  league: 'NCAAF' | 'NFL' | 'MLB' | 'NBA';
  /** PickType by name — the BE projects `PickType.ToString()` into this field. */
  leagueType: PickType;
  useConfidencePoints: boolean;
  memberCount: number;
  avatarUrl: string | null;
  /**
   * The season the league belongs to (e.g. 2026). Server-authoritative; drives
   * the Standings season filter so the client groups leagues by season without
   * inferring it.
   */
  seasonYear: number;
  /**
   * Distinct week numbers the league has, ascending. Lets Standings source its
   * week list from this one call (including past-season / deactivated leagues
   * that /user/me omits). Empty for leagues with no weeks yet.
   */
  seasonWeeks: number[];
  /**
   * Non-null once the league's season has passed: read-only, and not cloneable.
   * Only populated when the caller opts in via `includeDeactivated`; the default
   * list omits those rows entirely, so this is null for every league mobile
   * currently fetches.
   */
  deactivatedUtc: string | null;
  /**
   * When the league was created (ISO 8601, server-authoritative off
   * PickemGroup.CreatedUtc). Drives the default newest-first sort on the My
   * Leagues screen; the A-Z option sorts by `name`.
   */
  createdUtc: string;
}

export interface CloneLeagueRequest {
  name: string;
  inviteMembers: boolean;
}

// Matches SportsData.Api.Application.UI.Leagues.Dtos.LeagueCreationGateDto.
export interface LeagueCreationGate {
  /** Backend Sport enum name, e.g. "FootballNcaa". */
  sport: string;
  /** UTC instant league creation opens for this sport. */
  opensUtc: string;
}

// Matches SportsData.Api.Application.UI.Leagues.Dtos.LeagueCreationAvailabilityDto.
export interface LeagueCreationAvailability {
  gates: LeagueCreationGate[];
}

export const leaguesApi = {
  // POST /ui/leagues/football/ncaa
  createFootballNcaaLeague: (payload: CreateFootballNcaaLeagueRequest) =>
    apiClient.post<CreateLeagueResponse>('/ui/leagues/football/ncaa', payload),

  // POST /ui/leagues/football/nfl
  createFootballNflLeague: (payload: CreateFootballNflLeagueRequest) =>
    apiClient.post<CreateLeagueResponse>('/ui/leagues/football/nfl', payload),

  // POST /ui/leagues/baseball/mlb — admin-gated on the BE.
  createBaseballMlbLeague: (payload: CreateBaseballMlbLeagueRequest) =>
    apiClient.post<CreateLeagueResponse>('/ui/leagues/baseball/mlb', payload),

  // GET /ui/leagues/discover — public leagues the caller isn't in and that
  // are still joinable (the BE filters deactivated + already-member; closed
  // ones may still appear, badged via isJoinable).
  getPublicLeagues: () =>
    apiClient.get<PublicLeague[]>('/ui/leagues/discover'),

  // GET /ui/leagues/{id} — league detail for the invite preview.
  getLeagueById: (id: string) =>
    apiClient.get<LeagueDetail>(`/ui/leagues/${id}`),

  // POST /ui/leagues/{id}/join — join a league by id.
  joinLeague: (id: string) =>
    apiClient.post<void>(`/ui/leagues/${id}/join`),

  // DELETE /ui/leagues/{id} — commissioner-only; the BE enforces role and
  // refuses e.g. leagues with recorded picks (surface its error message).
  deleteLeague: (id: string) =>
    apiClient.delete<void>(`/ui/leagues/${id}`),

  // POST /ui/leagues/{id}/invite — email an invitation to a non-member.
  sendInvite: (id: string, email: string, inviteeName: string | null = null) =>
    apiClient.post<void>(`/ui/leagues/${id}/invite`, {
      leagueId: id,
      email,
      inviteeName,
    }),

  // GET /ui/leagues/{id}/invite/search?q= — registered users invitable to the
  // league (BE requires >= 2 chars; excludes self, members, synthetic users).
  searchInviteableUsers: (id: string, q: string) =>
    apiClient.get<InviteableUser[]>(`/ui/leagues/${id}/invite/search`, {
      params: { q },
    }),

  // POST /ui/leagues/{id}/invite/user — invite a registered user (from
  // search). Triggers the LeagueInvite push, which deep-links into the mobile
  // league-invite screen; no email.
  inviteUser: (id: string, userId: string) =>
    apiClient.post<void>(`/ui/leagues/${id}/invite/user`, { userId }),

  // GET /ui/leagues — the current user's leagues. The BE excludes deactivated
  // (past-season) leagues unless includeDeactivated is passed; those rows come
  // back carrying a non-null deactivatedUtc so the caller can mark them
  // read-only.
  getUserLeagues: ({ includeDeactivated = false }: { includeDeactivated?: boolean } = {}) =>
    apiClient.get<LeagueSummary[]>('/ui/leagues', {
      params: includeDeactivated ? { includeDeactivated: true } : undefined,
    }),

  // POST /ui/leagues/{id}/clone — duplicate a league the user belongs to.
  // Copies config and regenerates the slate server-side; picks are NOT copied.
  // Returns the new league's id.
  cloneLeague: (id: string, payload: CloneLeagueRequest) =>
    apiClient.post<{ id: string }>(`/ui/leagues/${id}/clone`, payload),

  // GET /ui/leagues/creation-availability — sports currently gated from league
  // creation, each with the UTC instant it opens (only active gates; a sport not
  // listed is open). The create endpoints enforce the same gate server-side.
  getCreationAvailability: () =>
    apiClient.get<LeagueCreationAvailability>('/ui/leagues/creation-availability'),

  // GET /ui/leagues/invitations — pending invitations for the current user.
  getPendingInvitations: () =>
    apiClient.get<PendingInvitation[]>('/ui/leagues/invitations'),

  // POST /ui/leagues/invitations/{id}/accept — joins the league via the
  // standard join path (all join-policy gates apply). Returns the league id.
  acceptInvitation: (invitationId: string) =>
    apiClient.post<string>(`/ui/leagues/invitations/${invitationId}/accept`),

  // POST /ui/leagues/invitations/{id}/decline — drops it off the pending card.
  declineInvitation: (invitationId: string) =>
    apiClient.post<boolean>(`/ui/leagues/invitations/${invitationId}/decline`),
};
