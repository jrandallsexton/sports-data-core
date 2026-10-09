import React from "react";
import { FaRobot } from "react-icons/fa";
import "./LeaderboardPage.css";

// Lineup slot order (rosterLogic's SLOT_DEFS, minus the disabled DEF).
const SLOT_ORDER = ["QB", "RB1", "RB2", "WR1", "WR2", "TE", "FLEX", "K"];
const SLOT_LABEL = { RB1: "RB", RB2: "RB", WR1: "WR", WR2: "WR" };

const points = (value) => (value == null ? "–" : value.toFixed(1));

/**
 * Player Pick'em "By Week": every member's lineup for one week, players and
 * points instead of the team overview's games and picks. Members are
 * columns (as in LeagueWeekOverviewTable), slots are rows. Another member's
 * slot stays hidden until its game locks; the header says how many.
 * Data: PlayerPickemApi.getLeagueWeekLineups.
 */
function PlayerWeekLineupsTable({ data, currentUserId, loading = false, error = false }) {
  // Loading and failure are told apart from a genuinely empty week.
  if (loading) {
    return <div className="loading">Loading lineups...</div>;
  }
  if (error) {
    return <p>Couldn&rsquo;t load lineups for this week. Try again shortly.</p>;
  }

  const members = data?.members ?? [];
  if (members.length === 0) {
    return <p>No lineups for this week.</p>;
  }

  const slotFor = (member, slotId) => member.slots.find((s) => s.slotId === slotId);

  return (
    <table className="leaderboard-table">
      <thead>
        <tr>
          <th>Slot</th>
          {members.map((m) => (
            <th key={m.userId} className={m.userId === currentUserId ? "current-user-row" : ""}>
              {m.isSynthetic && (
                <FaRobot className="robot-icon" style={{ marginRight: "6px", color: "#61dafb" }} />
              )}
              {m.displayName}
              {m.userId === currentUserId && <span className="you-label"> (You)</span>}
              {m.hiddenSlotCount > 0 && (
                <div style={{ fontWeight: "normal", fontSize: "0.8em", opacity: 0.7 }}>
                  🔒 {m.hiddenSlotCount} hidden until kickoff
                </div>
              )}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {SLOT_ORDER.map((slotId) => (
          <tr key={slotId}>
            <td>{SLOT_LABEL[slotId] ?? slotId}</td>
            {members.map((m) => {
              const slot = slotFor(m, slotId);
              return (
                <td key={m.userId}>
                  {slot ? (
                    <>
                      <div>
                        {slot.firstName.charAt(0)}. {slot.lastName}
                      </div>
                      <div style={{ fontSize: "0.85em", opacity: 0.75 }}>
                        {slot.teamName} · <strong>{points(slot.points)}</strong>
                      </div>
                      {/* The stats behind the points, as scored. */}
                      {slot.statLine ? (
                        <div style={{ fontSize: "0.8em", opacity: 0.65 }}>{slot.statLine}</div>
                      ) : null}
                    </>
                  ) : (
                    "–"
                  )}
                </td>
              );
            })}
          </tr>
        ))}
        <tr>
          <td>
            <strong>Total</strong>
          </td>
          {members.map((m) => (
            <td key={m.userId}>
              <strong>{m.totalPoints.toFixed(1)}</strong>
            </td>
          ))}
        </tr>
      </tbody>
    </table>
  );
}

export default PlayerWeekLineupsTable;
