import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import ResultsApi from "../../api/resultsApi";
import WeeklyScoreboard from "./WeeklyScoreboard";
import "./ResultsPage.css";

function pct(num, denom) {
  if (!denom) return "—";
  return ((100 * num) / denom).toFixed(1) + "%";
}

function sportLabel(sport, league) {
  const s = (sport || "").toLowerCase();
  const l = (league || "").toLowerCase();
  if (s === "football" && l === "ncaa") return "NCAA Football";
  if (s === "football" && l === "nfl") return "NFL";
  return `${sport} ${league}`.toUpperCase();
}

export default function ResultsPage() {
  const { sport, league, seasonYear } = useParams();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    ResultsApi.getSeasonResults(sport, league, seasonYear)
      .then((res) => {
        if (!cancelled) setData(res.data);
      })
      .catch((e) => {
        if (!cancelled) setError(e?.message ?? "Failed to load results");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [sport, league, seasonYear]);

  if (loading) return <div className="results-state">Loading…</div>;
  if (error) return <div className="results-state results-error">{error}</div>;
  if (!data) return <div className="results-state">No data.</div>;

  const a = data.aggregate;
  const suDenom = a.suWins + a.suLosses;
  const atsDenom = a.atsWins + a.atsLosses;

  return (
    <div className="results-page">
      <header className="results-hero">
        <h1>
          {sportLabel(data.sport, data.league)} {data.seasonYear} — AI vs Reality
        </h1>
        <div className="hero-stats">
          <div className="stat">
            <div className="stat-label">Straight Up</div>
            <div className="stat-value">
              {a.suWins}-{a.suLosses}
              <span className="stat-pct">{pct(a.suWins, suDenom)}</span>
            </div>
          </div>
          <div className="stat">
            <div className="stat-label">Against the Spread</div>
            <div className="stat-value">
              {a.atsWins}-{a.atsLosses}
              {a.atsPushes ? `-${a.atsPushes}` : ""}
              <span className="stat-pct">{pct(a.atsWins, atsDenom)}</span>
            </div>
          </div>
          <div className="stat">
            <div className="stat-label">Games Graded</div>
            <div className="stat-value">{a.totalGames}</div>
          </div>
        </div>
      </header>

      {data.weeks.length === 0 ? (
        <div className="results-state">No graded games for this season yet.</div>
      ) : (
        <main className="weeks-stack">
          {data.weeks.map((w) => (
            <WeeklyScoreboard key={`${w.weekNumber}-${w.seasonWeekEndDate}`} week={w} />
          ))}
        </main>
      )}
    </div>
  );
}
