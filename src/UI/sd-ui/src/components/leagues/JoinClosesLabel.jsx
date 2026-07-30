import { useEffect, useState } from "react";

// Live countdown only inside this window — "closes in 4 months" is noise.
// Operator-set threshold (2026-07-30): ~10 days.
const COUNTDOWN_WINDOW_MS = 10 * 24 * 60 * 60 * 1000;

// Minute granularity: day/hour/minute countdowns don't need seconds, and a
// 60s tick keeps idle browse tabs cheap.
const TICK_MS = 60 * 1000;

const formatRemaining = (ms) => {
  const totalMinutes = Math.max(0, Math.floor(ms / 60000));
  const days = Math.floor(totalMinutes / (60 * 24));
  const hours = Math.floor((totalMinutes % (60 * 24)) / 60);
  const minutes = totalMinutes % 60;
  if (days > 0) return `${days}d ${hours}h`;
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
};

/**
 * Renders when joining a league closes, fed by the BE's stored
 * invitationsExpireUtc (surfaced as closesAtUtc):
 *   - closed        -> "Closed"
 *   - > 10 days out -> plain date ("Closes Sep 15")
 *   - <= 10 days    -> live countdown, minute tick ("Closes in 2d 4h")
 *   - no value      -> "Open"
 */
function JoinClosesLabel({ closesAtUtc, isJoinable }) {
  const [now, setNow] = useState(() => Date.now());

  const closesMs = closesAtUtc ? new Date(closesAtUtc).getTime() : NaN;
  const remaining = closesMs - now;
  const inCountdownWindow =
    Number.isFinite(closesMs) && remaining > 0 && remaining <= COUNTDOWN_WINDOW_MS;

  useEffect(() => {
    if (!inCountdownWindow) return undefined;
    const id = setInterval(() => setNow(Date.now()), TICK_MS);
    return () => clearInterval(id);
  }, [inCountdownWindow]);

  if (isJoinable === false || (Number.isFinite(closesMs) && remaining <= 0)) {
    return <span className="join-closes join-closes--closed">Closed</span>;
  }

  if (!Number.isFinite(closesMs)) {
    return <span className="join-closes">Open</span>;
  }

  if (inCountdownWindow) {
    return (
      <span className="join-closes join-closes--countdown">
        Closes in {formatRemaining(remaining)}
      </span>
    );
  }

  const d = new Date(closesMs);
  return (
    <span className="join-closes">
      Closes {d.toLocaleDateString(undefined, { month: "short", day: "numeric" })}
    </span>
  );
}

export default JoinClosesLabel;
