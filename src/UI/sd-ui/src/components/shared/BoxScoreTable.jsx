import React from "react";
import "./BoxScoreTable.css";

/**
 * Compact period-by-period scoreboard. Sport-agnostic: works for football
 * quarters (4, +OT) and baseball innings (9+) — anything past 4 periods
 * triggers a tighter "wide" cell padding so the Total ("T") column doesn't
 * collide with adjacent header content.
 *
 * Originally inline JSX inside ContestOverviewHeader. Lifted so live-state
 * matchup cards (Command Center / picks page when game is in progress) can
 * reuse the same widget without forking it.
 *
 * @param {object} props
 * @param {Array<{ quarter: string|number, awayScore: number, homeScore: number }>} props.periodScores
 *   One entry per period, in order. The shape's `quarter` field name is
 *   historical — semantically it's the period label (Q1 / I3 / etc.) and
 *   is rendered verbatim as the column header.
 * @param {string} props.awayLabel - Short label for away row (e.g., "TEX", "ANGELS")
 * @param {string} props.homeLabel - Short label for home row
 * @param {string} [props.statusLabel="Final"] - Label rendered above the table
 *   (e.g., "Final", "Live · B5", "Top 3rd"). Caller computes from game state.
 */
export default function BoxScoreTable({
  periodScores,
  awayLabel,
  homeLabel,
  statusLabel = "Final",
}) {
  const awayTotal = periodScores.reduce((sum, p) => sum + p.awayScore, 0);
  const homeTotal = periodScores.reduce((sum, p) => sum + p.homeScore, 0);
  const wideClass = periodScores.length > 4 ? " wide" : "";

  return (
    <div className={`boxscore-table-wrapper compact${wideClass}`}>
      <div className="boxscore-status">{statusLabel}</div>
      <table className={`boxscore-table compact${wideClass}`}>
        <thead>
          <tr>
            <th></th>
            {periodScores.map((p) => (
              <th key={p.quarter}>{p.quarter}</th>
            ))}
            <th>T</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td className="boxscore-team-short">{awayLabel}</td>
            {periodScores.map((p) => (
              <td key={p.quarter}>{p.awayScore}</td>
            ))}
            <td className="boxscore-total">{awayTotal}</td>
          </tr>
          <tr>
            <td className="boxscore-team-short">{homeLabel}</td>
            {periodScores.map((p) => (
              <td key={p.quarter}>{p.homeScore}</td>
            ))}
            <td className="boxscore-total">{homeTotal}</td>
          </tr>
        </tbody>
      </table>
    </div>
  );
}
