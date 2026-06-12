import ScorebugTile from "./ScorebugTile";
import "./ResultsPage.css";

function recordString(agg) {
  return `SU ${agg.suWins}-${agg.suLosses}   ATS ${agg.atsWins}-${agg.atsLosses}${
    agg.atsPushes ? `-${agg.atsPushes}` : ""
  }`;
}

function endDateLabel(iso) {
  if (!iso) return "";
  // Backend ships SeasonWeekEndDate as midnight UTC. Format in UTC so
  // viewers west of UTC don't see the date roll back to the prior day.
  const d = new Date(iso);
  return d.toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  });
}

export default function WeeklyScoreboard({ week }) {
  return (
    <section className="weekly-row">
      <header className="weekly-header">
        <div className="weekly-title">
          <span className="week-label">Week {week.weekNumber}</span>
          <span className="week-subtitle">ending {endDateLabel(week.seasonWeekEndDate)}</span>
        </div>
        <div className="weekly-record">{recordString(week.aggregate)}</div>
      </header>
      <div className="tile-grid">
        {week.games.map((g) => (
          <ScorebugTile key={g.contestId} game={g} />
        ))}
      </div>
    </section>
  );
}
