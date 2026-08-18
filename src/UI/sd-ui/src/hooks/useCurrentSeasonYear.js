import { useEffect, useState } from "react";
import SeasonApi from "../api/seasonApi";

/**
 * Resolves the current (or upcoming) season year for a sport from
 * /api/{sport}/{league}/seasons/current — the same source the home-page
 * countdown uses. Exists so rankings surfaces never hardcode a season
 * year again: the 2025 literals in the old RankingsWidget silently froze
 * it to a finished season at rollover.
 *
 * Returns { seasonYear, loading }. seasonYear stays null when no season
 * is sourced (a valid state for an unsupported sport, not an error).
 */
export default function useCurrentSeasonYear(sport = "football", league = "ncaa") {
  const [seasonYear, setSeasonYear] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    SeasonApi.getCurrentSeason(sport, league)
      .then((res) => {
        if (!cancelled) setSeasonYear(res.data?.seasonYear ?? null);
      })
      .catch(() => {
        if (!cancelled) setSeasonYear(null);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [sport, league]);

  return { seasonYear, loading };
}
