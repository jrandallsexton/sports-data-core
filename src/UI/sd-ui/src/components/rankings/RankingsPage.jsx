import { useParams } from "react-router-dom";
import RankingsWidget from "../widgets/RankingsWidget";
import "./RankingsPage.css";

/**
 * Sport-scoped rankings surface:
 *   /app/sport/:sport/:league/rankings                    current season, latest polls
 *   /app/sport/:sport/:league/rankings/:seasonYear        that season's latest polls
 *   /app/sport/:sport/:league/rankings/:seasonYear/week/:week  a specific week
 *
 * Only football/ncaa has poll data today, and the backend week endpoint is
 * not yet scope-aware (multi-sport TODO in GetRankingsByPollWeekQueryHandler)
 * — so any other tuple gets an honest empty state here rather than silently
 * rendering NCAAFB data under an NFL/MLB URL.
 *
 * Params are validated here (year/week must be positive integers) so the
 * widget only ever receives clean values — garbage segments fall back to
 * the no-param behavior rather than producing a broken API call.
 */
const SUPPORTED_SCOPES = [{ sport: "football", league: "ncaa" }];

function RankingsPage() {
  const { sport, league, seasonYear, week } = useParams();

  const isSupported = SUPPORTED_SCOPES.some(
    (s) => s.sport === sport?.toLowerCase() && s.league === league?.toLowerCase()
  );

  if (!isSupported) {
    return (
      <div className="rankings-page">
        <p className="rankings-page__unsupported">
          Rankings aren't available for this league yet.
        </p>
      </div>
    );
  }

  const parsedYear =
    /^\d{4}$/.test(seasonYear ?? "") && Number(seasonYear) > 0
      ? Number(seasonYear)
      : undefined;
  const parsedWeek =
    parsedYear && /^\d{1,2}$/.test(week ?? "") && Number(week) > 0
      ? Number(week)
      : undefined;

  return (
    <div className="rankings-page">
      <RankingsWidget
        sport={sport}
        league={league}
        seasonYear={parsedYear}
        week={parsedWeek}
      />
    </div>
  );
}

export default RankingsPage;
