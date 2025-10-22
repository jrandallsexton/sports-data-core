import React, { useState } from "react";
import { FaSearchPlus, FaSearchMinus } from "react-icons/fa";
import { Link } from "react-router-dom";
import { formatToMonthDay } from "../../utils/timeUtils";
import "./MiniSchedule.css";
import "./MiniScheduleDrilldown.css";

function formatGameResult(game) {
  if (!game.finalizedUtc) return "TBD";
  const score = `${game.awayScore}-${game.homeScore}`;
  const resultText = game.wasWinner ? "W" : "L";
  return `${resultText} | ${score}`;
}

function getResultClass(game) {
  if (!game.finalizedUtc) return "result-tbd";
  return game.wasWinner ? "result-win" : "result-loss";
}

export default function MiniSchedule({ schedule = [], seasonYear }) {
  // TODO: create new endpoint that only returns completed games
  const games = schedule.slice(0, 7);
  // Drilldown state: which row is open, and its data
  const [drillIndex, setDrillIndex] = useState(null);
  const [drillSchedule, setDrillSchedule] = useState([]);
  const [drillLoading, setDrillLoading] = useState(false);
  const [drillError, setDrillError] = useState(null);

  // Handler for drilldown icon click
  const handleDrillClick = async (idx, opponentSlug) => {
    if (drillIndex === idx) {
      setDrillIndex(null);
      setDrillSchedule([]);
      setDrillError(null);
      return;
    }
    setDrillIndex(idx);
    setDrillLoading(true);
    setDrillError(null);
    setDrillSchedule([]);
    try {
      // Use current seasonYear for opponent
      const res = await import("../../api/apiWrapper").then(m => m.default.TeamCard.getBySlugAndSeason(opponentSlug, seasonYear));
      setDrillSchedule(Array.isArray(res.data?.schedule) ? res.data.schedule.slice(0, 5) : []);
    } catch (e) {
      setDrillError("Failed to load schedule");
    } finally {
      setDrillLoading(false);
    }
  };

  return (
    <div className="mini-schedule">
      <table>
        <thead>
          <tr>
            <th>Date</th>
            <th>Opponent</th>
            <th>Result</th>
          </tr>
        </thead>
        <tbody>
          {games.length ? (
            games.map((game, idx) => [
              <tr key={idx}>
                <td>{formatToMonthDay(game.date)}</td>
                <td style={{ display: 'flex', alignItems: 'center' }}>
                  <Link to={`/app/sport/football/ncaa/team/${game.opponentSlug}/${seasonYear}`} className="team-link">
                    {game.opponentShortName}
                  </Link>
                  {game.opponentSlug && (
                    <button
                      className="mini-schedule-drill-icon-btn"
                      aria-label={drillIndex === idx ? `Hide last 5 for ${game.opponent}` : `Show last 5 for ${game.opponent}`}
                      onClick={() => handleDrillClick(idx, game.opponentSlug)}
                      title={drillIndex === idx ? `Hide last 5 for ${game.opponent}` : `Show last 5 for ${game.opponent}`}
                      style={{ marginLeft: 6 }}
                    >
                      {drillIndex === idx ? (
                        <FaSearchMinus style={{ fontSize: '1.1em', verticalAlign: 'middle' }} aria-label="Hide last 5 games" />
                      ) : (
                        <FaSearchPlus style={{ fontSize: '1.1em', verticalAlign: 'middle' }} aria-label="Show last 5 games" />
                      )}
                    </button>
                  )}
                </td>
                <td>
                  {game.finalizedUtc && game.contestId ? (
                    <Link
                      to={`/app/sport/football/ncaa/contest/${game.contestId}`}
                      className={`result-link ${getResultClass(game)}`}
                    >
                      {formatGameResult(game)}
                    </Link>
                  ) : (
                    <span className={getResultClass(game)}>{formatGameResult(game)}</span>
                  )}
                </td>
              </tr>,
              drillIndex === idx && (
                <tr key={`drilldown-${idx}`} className="mini-schedule-drilldown-row">
                  <td colSpan={3} className="mini-schedule-drilldown-cell">
                    {drillLoading ? (
                      <div style={{ padding: 6, fontSize: '0.95em' }}>Loading…</div>
                    ) : drillError ? (
                      <div style={{ padding: 6, color: 'red', fontSize: '0.95em' }}>{drillError}</div>
                    ) : (
                      <MiniSchedule schedule={drillSchedule} seasonYear={seasonYear} />
                    )}
                  </td>
                </tr>
              )
            ])
          ) : (
            <tr><td colSpan={3}>No recent games</td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
