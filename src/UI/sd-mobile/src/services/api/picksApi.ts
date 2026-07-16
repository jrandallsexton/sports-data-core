import { apiClient } from './client';
import type { UserPick, PickType, PickWidgetResponse } from '@/src/types/models';

export interface SubmitPickPayload {
  pickemGroupId: string;      // leagueId
  contestId: string;          // matchup's contestId
  pickType: PickType;
  franchiseSeasonId: string;  // picked team's franchiseSeasonId
  week: number;
  confidencePoints?: number;
}

// ─── Cross-league pick import ───────────────────────────────────────────────

/** A candidate league to import picks from (same type, shares >=1 contest). */
export interface PickImportSource {
  leagueId: string;
  name: string;
  sport?: string;
  pickType?: PickType;
  useConfidencePoints?: boolean;
  sharedContestCount?: number;
  memberCount?: number;
}

/** A single importable pick in a preview (an unpicked target contest). */
export interface PickImportItem {
  contestId: string;
  week: number;
  franchiseSeasonId: string;
  headline?: string | null;
  targetHomeSpread?: number | null;
}

export interface PickImportPreview {
  sourceLeagueId: string;
  targetLeagueId: string;
  targetUsesConfidencePoints: boolean;
  toImport: PickImportItem[];
  collisions: unknown[];
  skipped: unknown[];
}

export interface PickImportResult {
  imported: number;
  replaced: number;
  skipped: number;
  skippedByReason: Record<string, number>;
  requiresConfidence: boolean;
  draft: PickImportItem[];
}

export const picksApi = {
  // GET /ui/picks/{leagueId}/week/{week}
  getByLeagueAndWeek: (leagueId: string, week: number) =>
    apiClient.get<UserPick[]>(`/ui/picks/${leagueId}/week/${week}`),

  // POST /ui/picks
  submitPick: (payload: SubmitPickPayload) =>
    apiClient.post<void>('/ui/picks', payload),

  // GET /ui/picks/{year}/widget  — season-to-date pick record for the current user
  getWidget: (year = 2025) =>
    apiClient.get<PickWidgetResponse>(`/ui/picks/${year}/widget`),

  // GET /ui/leagues/{targetId}/picks/import/sources
  getImportSources: (leagueId: string) =>
    apiClient.get<PickImportSource[]>(
      `/ui/leagues/${leagueId}/picks/import/sources`,
    ),

  // POST /ui/leagues/{targetId}/picks/import/preview
  getImportPreview: (leagueId: string, sourceLeagueId: string) =>
    apiClient.post<PickImportPreview>(
      `/ui/leagues/${leagueId}/picks/import/preview`,
      { sourceLeagueId },
    ),

  // POST /ui/leagues/{targetId}/picks/import
  executeImport: (leagueId: string, sourceLeagueId: string, contestIds: string[]) =>
    apiClient.post<PickImportResult>(
      `/ui/leagues/${leagueId}/picks/import`,
      { sourceLeagueId, contestIds },
    ),
};
