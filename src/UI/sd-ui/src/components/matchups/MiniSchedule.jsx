import React, { useState } from "react";
import { FaSearchPlus, FaSearchMinus } from "react-icons/fa";
import { Link } from "react-router-dom";
import { formatToMonthDay } from "../../utils/timeUtils";
import { useUserTimeZone } from "../../hooks/useUserTimeZone";
import "./MiniSchedule.css";
import { teamLink, contestLink, resolveSportLeague } from '../../utils/sportLinks';
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

export default function MiniSchedule({ schedule = [], seasonYear, leagueSport }) {
  // Null when the backend sport enum is missing or unmapped — we then render
  // plain-text team names instead of risking a wrong-sport route.
  const sportLeague = resolveSportLeague(leagueSport);
  const userTz = useUserTimeZone();
  // TODO: create new endpoint that only returns completed games
  const games = schedule.slice(0, 13);
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
    if (!sportLeague) {
      setDrillError("Schedule unavailable for this sport.");
      setDrillLoading(false);
      return;
    }
    try {
      // Use current seasonYear for opponent
      const res = await import("../../api/apiWrapper").then(m =>
        m.default.TeamCard.getBySlugAndSeason(sportLeague.sport, sportLeague.league, opponentSlug, seasonYear));
      setDrillSchedule(Array.isArray(res.data?.schedule) ? res.data.schedule.slice(0, 13) : []);
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
                <td>{formatToMonthDay(game.date, userTz)}</td>
                <td style={{ display: 'flex', alignItems: 'center' }}>
                  {(() => {
                    const opponentLabel = game.opponentShortName ?? game.opponent ?? 'Opponent';
                    const displayLabel = game.locationType === 'Away' ? `@ ${opponentLabel}` : opponentLabel;
                    // Only render a Link when we have a slug AND a resolvable
                    // sport/league; otherwise fall back to a plain span with the
                    // same class/label so the row still reads correctly without
                    // producing /team/undefined or a wrong-sport route.
                    return game.opponentSlug && sportLeague ? (
                      <Link to={teamLink(game.opponentSlug, seasonYear, sportLeague.sport, sportLeague.league)} className="team-link">
                        {displayLabel}
                      </Link>
                    ) : (
                      <span className="team-link">{displayLabel}</span>
                    );
                  })()}
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
                  {game.finalizedUtc && game.contestId && sportLeague ? (
                    <Link
                      to={contestLink(game.contestId, sportLeague.sport, sportLeague.league)}
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
                      <MiniSchedule schedule={drillSchedule} seasonYear={seasonYear} leagueSport={leagueSport} />
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
