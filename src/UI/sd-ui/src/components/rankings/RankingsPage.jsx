import { useParams } from "react-router-dom";
import RankingsWidget from "../widgets/RankingsWidget";
import "./RankingsPage.css";

/**
 * Sport-scoped rankings surface:
 *   /app/sport/:sport/:league/rankings                    current season, latest polls
 *   /app/sport/:sport/:league/rankings/:seasonYear        that season's latest polls
 *   /app/sport/:sport/:league/rankings/:seasonYear/week/:week  a specific week
 *
 * Params are validated here (year/week must be positive integers) so the
 * widget only ever receives clean values — garbage segments fall back to
 * the no-param behavior rather than producing a broken API call.
 */
function RankingsPage() {
  const { sport, league, seasonYear, week } = useParams();

  const parsedYear = /^\d{4}$/.test(seasonYear ?? "") ? Number(seasonYear) : undefined;
  const parsedWeek =
    parsedYear && /^\d{1,2}$/.test(week ?? "") ? Number(week) : undefined;

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
