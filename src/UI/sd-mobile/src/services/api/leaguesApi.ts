import { apiClient } from './client';

// Matches SportsData.Api.Application.Common.Enums.PickType (by name).
export type PickType = 'StraightUp' | 'AgainstTheSpread' | 'OverUnder';

// Matches SportsData.Api.Application.Common.Enums.TiebreakerType.
export type TiebreakerType = 'TotalPoints' | 'HomeAndAwayScores' | 'EarliestSubmission';

// Matches SportsData.Api.Application.Common.Enums.TiebreakerTiePolicy.
// Only one value today; kept as a union so adding more is a no-code-change lift.
export type TiebreakerTiePolicy = 'EarliestSubmission';

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
  members: LeagueMember[];
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
   * Non-null once the league's season has passed: read-only, and not cloneable.
   * Only populated when the caller opts in via `includeDeactivated`; the default
   * list omits those rows entirely, so this is null for every league mobile
   * currently fetches.
   */
  deactivatedUtc: string | null;
}

export interface CloneLeagueRequest {
  name: string;
  inviteMembers: boolean;
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

  // GET /ui/leagues/{id} — league detail for the invite preview.
  getLeagueById: (id: string) =>
    apiClient.get<LeagueDetail>(`/ui/leagues/${id}`),

  // POST /ui/leagues/{id}/join — join a league by id.
  joinLeague: (id: string) =>
    apiClient.post<void>(`/ui/leagues/${id}/join`),

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
};
