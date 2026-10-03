import { useState, useEffect, useRef, useMemo, useCallback } from "react";
import { Bot } from "lucide-react";
import apiWrapper from "../../api/apiWrapper.js";
import {
  ADVISOR_LEVELS,
  applicablePicks,
  applyLabel,
  describePick,
  describeStanding,
  describeStatBot,
  levelMeta,
} from "./advisorSheet";
import "./StatBotAdvisorDialog.css";

/**
 * StatBot advisor — standings analysis, a risk level (StatBot's
 * recommendation pre-selected), and the full suggested sheet, with one
 * Apply that replaces every unlocked pick. Read-only until Apply; the
 * parent writes through the normal submit path.
 *
 * The dialog knows exactly what the user knows: the endpoint never reads
 * another member's picks (rule zero). See docs/features/statbot-advisor.md.
 */
function StatBotAdvisorDialog({
  isOpen,
  leagueId,
  week,
  matchups,
  userPicks,
  useConfidencePoints,
  applying,
  onClose,
  onApply,
}) {
  // Advice per level, keyed by level; "recommended" holds the first fetch.
  const [adviceByLevel, setAdviceByLevel] = useState({});
  const [selectedLevel, setSelectedLevel] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const dialogRef = useRef(null);
  // Monotonic request id: only the LATEST request may write selection or
  // clear loading. Clicking through the level cards fires overlapping
  // fetches, and without this the last response to land — not the last
  // click — decided what Apply would submit.
  const requestRef = useRef(0);

  const fetchAdvice = useCallback(
    async (level) => {
      const id = ++requestRef.current;
      setLoading(true);
      setError(null);
      try {
        const res = await apiWrapper.Picks.getAdvice(leagueId, week, level);
        const advice = res.data;
        // Every response is worth caching; only the latest may steer.
        setAdviceByLevel((prev) => ({ ...prev, [advice.level]: advice }));
        if (id === requestRef.current) setSelectedLevel(advice.level);
      } catch (err) {
        if (id !== requestRef.current) return;
        console.error("StatBot advice failed:", err);
        setError("StatBot couldn't put a sheet together. Try again in a moment.");
      } finally {
        if (id === requestRef.current) setLoading(false);
      }
    },
    [leagueId, week]
  );

  // First open: no level → the server picks one and tells us which.
  useEffect(() => {
    if (!isOpen) return;
    setAdviceByLevel({});
    setSelectedLevel(null);
    fetchAdvice(null);
  }, [isOpen, fetchAdvice]);

  // Close on Escape (unless mid-apply), matching the import dialog.
  useEffect(() => {
    const onKey = (e) => {
      if (e.key === "Escape" && !applying) onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [applying, onClose]);

  // Move focus into the dialog on open; restore it to the trigger on close.
  useEffect(() => {
    const previouslyFocused = document.activeElement;
    dialogRef.current?.focus();
    return () => {
      if (previouslyFocused instanceof HTMLElement) previouslyFocused.focus();
    };
  }, []);

  const byContest = useMemo(
    () => new Map((matchups ?? []).map((m) => [m.contestId, m])),
    [matchups]
  );

  if (!isOpen) return null;

  const handleTabTrap = (e) => {
    if (e.key !== "Tab") return;
    const focusables = dialogRef.current?.querySelectorAll(
      'button:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'
    );
    if (!focusables || focusables.length === 0) return;
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    const atStart =
      document.activeElement === first || document.activeElement === dialogRef.current;
    if (e.shiftKey && atStart) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first.focus();
    }
  };

  const advice = selectedLevel ? adviceByLevel[selectedLevel] : null;
  // Recommendation and analysis are level-independent; read them from any
  // cached response so the card doesn't blank while a new level loads.
  const anyAdvice = advice ?? Object.values(adviceByLevel)[0] ?? null;
  const recommendedLevel = anyAdvice?.recommendedLevel ?? null;
  const analysis = anyAdvice?.analysis ?? null;

  const chooseLevel = (key) => {
    // A selected level with no advice is a failed fetch: clicking its card
    // again is the retry the error message asks for, so let it through.
    if (applying || (key === selectedLevel && adviceByLevel[key])) return;
    // The click decides the selection immediately; an uncached level shows
    // "thinking…" (no advice for it yet, Apply disabled) until ITS response
    // lands. A stale response for another level only fills the cache.
    setSelectedLevel(key);
    if (!adviceByLevel[key]) {
      fetchAdvice(key);
    } else {
      requestRef.current++; // a cached pick supersedes any fetch in flight
      setLoading(false);
      setError(null); // a stale failure from another level must not sit over a good sheet
    }
  };

  const teamName = (contestId, franchiseSeasonId) => {
    const m = byContest.get(contestId);
    if (!m || !franchiseSeasonId) return null;
    if (franchiseSeasonId === m.homeFranchiseSeasonId) return m.home ?? m.homeShort;
    if (franchiseSeasonId === m.awayFranchiseSeasonId) return m.away ?? m.awayShort;
    return null;
  };

  const matchupLabel = (pick) => {
    const m = byContest.get(pick.contestId);
    return m ? `${m.away} @ ${m.home}` : pick.headline ?? "";
  };

  const toApply = applicablePicks(advice?.picks, useConfidencePoints);
  const applyCount = toApply.length;

  return (
    <div className="advisor-dialog-overlay" onClick={applying ? undefined : onClose}>
      <div
        className="advisor-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="advisor-dialog-title"
        aria-busy={loading || applying}
        ref={dialogRef}
        tabIndex={-1}
        onKeyDown={handleTabTrap}
        onClick={(e) => e.stopPropagation()}
      >
        <h3 id="advisor-dialog-title" className="advisor-dialog-title">
          <Bot className="advisor-dialog-title-icon" aria-hidden="true" />
          StatBot advisor
        </h3>

        {error && <p className="advisor-dialog-error">{error}</p>}

        {analysis && (
          <section className="advisor-analysis" aria-label="Your standing">
            <p className="advisor-analysis-headline">{describeStanding(analysis)}</p>
            <dl className="advisor-analysis-grid">
              <div>
                <dt>Rank</dt>
                <dd>{analysis.rank != null ? `#${analysis.rank} of ${analysis.memberCount}` : "—"}</dd>
              </div>
              <div>
                <dt>Points / game</dt>
                <dd>
                  {analysis.pointsPerGame}
                  <span className="advisor-analysis-vs"> vs {analysis.leaderPointsPerGame}</span>
                </dd>
              </div>
              <div>
                <dt>Total</dt>
                <dd>
                  {analysis.totalPoints}
                  <span className="advisor-analysis-vs"> vs {analysis.leaderTotalPoints}</span>
                </dd>
              </div>
              <div>
                <dt>This week</dt>
                <dd title="Open games this week and the most points they can yield">
                  {analysis.gamesThisWeek} game{analysis.gamesThisWeek === 1 ? "" : "s"}
                  <span className="advisor-analysis-sub">
                    max {analysis.maxPointsThisWeek} pts
                    {analysis.bestCaseRankThisWeek != null && ` · best #${analysis.bestCaseRankThisWeek}`}
                  </span>
                </dd>
              </div>
            </dl>
            {describeStatBot(analysis) && (
              <p className="advisor-analysis-statbot">{describeStatBot(analysis)}</p>
            )}
          </section>
        )}

        <div className="advisor-levels" role="radiogroup" aria-label="Risk level">
          {ADVISOR_LEVELS.map((l) => {
            const selected = l.key === selectedLevel;
            const recommended = l.key === recommendedLevel;
            return (
              <button
                key={l.key}
                type="button"
                role="radio"
                aria-checked={selected}
                className={`advisor-level${selected ? " selected" : ""}`}
                onClick={() => chooseLevel(l.key)}
                disabled={applying || (loading && !anyAdvice)}
              >
                <span className="advisor-level-name">
                  {l.name}
                  {recommended && <span className="advisor-level-call">StatBot's call</span>}
                </span>
                <span className="advisor-level-tagline">{l.tagline}</span>
              </button>
            );
          })}
        </div>

        {advice && (
          <p className="advisor-level-detail">{levelMeta(advice.level).detail}</p>
        )}

        <div className="advisor-sheet" aria-live="polite">
          {!advice && !error && <p className="advisor-dialog-message">StatBot is thinking…</p>}
          {advice &&
            advice.picks.map((pick) => {
              const team = teamName(pick.contestId, pick.franchiseSeasonId);
              return (
                <div
                  key={pick.contestId}
                  className={`advisor-row kind-${pick.kind.toLowerCase()}`}
                >
                  <div className="advisor-row-main">
                    <span className="advisor-row-matchup">{matchupLabel(pick)}</span>
                    <span className="advisor-row-team">{team ?? "—"}</span>
                    {useConfidencePoints && pick.confidencePoints != null && (
                      <span className="advisor-row-points" title="Confidence points">
                        {pick.confidencePoints}
                      </span>
                    )}
                  </div>
                  <div className="advisor-row-sub">
                    <span className="advisor-row-kind">{kindLabel(pick.kind)}</span>
                    <span className="advisor-row-reason">{describePick(pick)}</span>
                    {/* Only when the user HAS a pick here and it differs. A blank
                        slate is "set", not "changed" — 21 CHANGES badges on an
                        unpicked week read as if something was being overwritten. */}
                    {pick.differsFromExisting &&
                      pick.kind !== "Locked" &&
                      pick.kind !== "NoPrediction" &&
                      userPicks?.[pick.contestId]?.franchiseSeasonId && (
                        <span className="advisor-row-change" title="Applying will change your current pick">
                          changes
                        </span>
                      )}
                  </div>
                </div>
              );
            })}
        </div>

        {advice && advice.noPredictionCount > 0 && (
          <p className="advisor-dialog-note">
            {advice.noPredictionCount} game{advice.noPredictionCount === 1 ? " has" : "s have"} no
            deetsMeter number, so StatBot leaves {advice.noPredictionCount === 1 ? "it" : "them"} to
            you. A pick you already have there stays as it is; otherwise the lowest values are held
            back for it.
          </p>
        )}

        <div className="advisor-dialog-buttons">
          <button className="advisor-dialog-button cancel" onClick={onClose} disabled={applying}>
            Cancel
          </button>
          <button
            className="advisor-dialog-button confirm"
            onClick={() => onApply(toApply, advice?.level)}
            disabled={applying || loading || !advice || applyCount === 0}
            title={applyCount === 0 && advice ? "Your picks already match this sheet" : undefined}
          >
            {applying ? "Applying…" : applyLabel(toApply, userPicks)}
          </button>
        </div>
      </div>
    </div>
  );
}

function kindLabel(kind) {
  switch (kind) {
    case "Lock":
      return "Lock";
    case "Lean":
      return "Lean";
    case "Flip":
      return "Flip";
    case "Locked":
      return "Locked";
    case "NoPrediction":
      return "No call";
    default:
      return kind;
  }
}

export default StatBotAdvisorDialog;
