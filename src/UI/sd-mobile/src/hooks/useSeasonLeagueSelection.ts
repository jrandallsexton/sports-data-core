import { Dispatch, SetStateAction, useEffect, useMemo, useState } from 'react';
import type { LeagueSummary } from '@/src/services/api/leaguesApi';

export interface SeasonLeagueSelection {
  /** All seasons the user has leagues in, newest-first. */
  seasons: number[];
  selectedSeason: number | null;
  setSelectedSeason: (year: number) => void;
  /** Leagues within the selected season, filtered by the active/ended toggle. */
  seasonLeagues: LeagueSummary[];
  selectedLeagueId: string | null;
  setSelectedLeagueId: (id: string) => void;
  /** Whether the "Show ended" toggle applies (current season with active leagues). */
  canFilterEnded: boolean;
  showEnded: boolean;
  setShowEnded: Dispatch<SetStateAction<boolean>>;
}

/**
 * Season/league selection state machine for the Standings screen. Pure aside
 * from its own local state: derives the season list from allLeagues (server-
 * authoritative seasonYear), applies the active/ended filter, and reconciles the
 * selected season + league so they stay valid as inputs change. Extracted from
 * the screen so this logic is independently unit-testable.
 *
 * Behavior preserved from the inline version:
 *  - Season list is newest-first, distinct across all leagues.
 *  - "Show ended" applies only to the current (newest) season that has active
 *    leagues; a prior season is all-ended so every league shows there, and the
 *    seasonHasActive guard avoids an empty row when even the current season has
 *    no active leagues.
 *  - selectedSeason falls back to the saved league's season (if it's one of
 *    theirs) else the newest.
 *  - selectedLeagueId snaps to the first visible league whenever it drops out of
 *    the current season/filter.
 */
export function useSeasonLeagueSelection(allLeagues: LeagueSummary[]): SeasonLeagueSelection {
  const [selectedLeagueId, setSelectedLeagueId] = useState<string | null>(null);
  const [selectedSeason, setSelectedSeason] = useState<number | null>(null);
  // Active-only by default to keep the league row short; the pill reveals ended.
  const [showEnded, setShowEnded] = useState(false);

  const seasons = useMemo(
    () => [...new Set(allLeagues.map((l) => l.seasonYear))].sort((a, b) => b - a),
    [allLeagues],
  );

  // Sorted by name (id tie-breaker) so the league list order and the
  // reconciliation snap target (seasonLeagues[0]) are deterministic —
  // getUserLeagues returns no guaranteed order.
  const seasonAllLeagues = useMemo(
    () =>
      selectedSeason == null
        ? []
        : allLeagues
            .filter((l) => l.seasonYear === selectedSeason)
            .sort((a, b) => a.name.localeCompare(b.name) || a.id.localeCompare(b.id)),
    [allLeagues, selectedSeason],
  );

  const isCurrentSeason = selectedSeason != null && selectedSeason === seasons[0];
  const seasonHasActive = useMemo(
    () => seasonAllLeagues.some((l) => !l.deactivatedUtc),
    [seasonAllLeagues],
  );
  const canFilterEnded = isCurrentSeason && seasonHasActive;

  const seasonLeagues = useMemo(
    () =>
      canFilterEnded && !showEnded
        ? seasonAllLeagues.filter((l) => !l.deactivatedUtc)
        : seasonAllLeagues,
    [seasonAllLeagues, canFilterEnded, showEnded],
  );

  // Keep selectedSeason valid: unset or no-longer-present snaps to the saved
  // league's season if it's one of theirs, otherwise the newest season.
  useEffect(() => {
    if (seasons.length === 0) return;
    if (selectedSeason != null && seasons.includes(selectedSeason)) return;
    const saved = allLeagues.find((l) => l.id === selectedLeagueId);
    setSelectedSeason(saved ? saved.seasonYear : seasons[0]);
  }, [seasons, selectedSeason, allLeagues, selectedLeagueId]);

  // Keep the selected league within the visible set (snap after switching season
  // or toggling the pill).
  useEffect(() => {
    if (selectedSeason == null || seasonLeagues.length === 0) return;
    if (!seasonLeagues.some((l) => l.id === selectedLeagueId)) {
      setSelectedLeagueId(seasonLeagues[0].id);
    }
  }, [selectedSeason, seasonLeagues, selectedLeagueId]);

  return {
    seasons,
    selectedSeason,
    setSelectedSeason,
    seasonLeagues,
    selectedLeagueId,
    setSelectedLeagueId,
    canFilterEnded,
    showEnded,
    setShowEnded,
  };
}
