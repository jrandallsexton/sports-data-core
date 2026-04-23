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
};
