import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import LeaguesApi from "api/leagues/leaguesApi";
import JoinClosesLabel from "../leagues/JoinClosesLabel";
import JoinLeagueConfirmDialog from "../leagues/JoinLeagueConfirmDialog";
import "./JoinableLeaguesCard.css";

const SPORT_LABEL = {
  FootballNcaa: "NCAAFB",
  FootballNfl: "NFL",
  BaseballMlb: "MLB",
};

// Same glyph convention as YourLeaguesCard — stand-in until per-league icons.
const SPORT_ICON = {
  FootballNcaa: "🏈",
  FootballNfl: "🏈",
  BaseballMlb: "⚾",
};

const MAX_ROWS = 4;

/**
 * Tier 3 — "Leagues you can join". Public-league discovery surfaced directly
 * on the home page so a new (or league-less) user lands on actionable content
 * instead of a bare countdown. Shows joinable leagues only — the full discover
 * page handles closed ones (badged there). Renders nothing while loading or
 * when there is nothing joinable, so the home page never shows an empty shell.
 */
function JoinableLeaguesCard() {
  const navigate = useNavigate();
  const [leagues, setLeagues] = useState(null);
  // Same confirmation the discover page uses — both join surfaces show
  // identical details before committing.
  const [confirming, setConfirming] = useState(null);

  useEffect(() => {
    let cancelled = false;
    LeaguesApi.getPublicLeagues()
      .then((data) => {
        if (!cancelled) setLeagues((data || []).filter((l) => l.isJoinable));
      })
      .catch((err) => {
        console.error("Failed to load joinable leagues:", err);
        if (!cancelled) setLeagues([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  if (!leagues || leagues.length === 0) return null;

  const visible = leagues.slice(0, MAX_ROWS);

  return (
    <section className="joinable-leagues-card">
      <div className="joinable-leagues-header">
        <h3>Leagues you can join</h3>
        {/* "›" matches YourLeaguesCard's "Manage ›" (itself mirroring
            sd-mobile) — one chevron convention across the home cards. */}
        <Link to="/app/league/discover" className="joinable-leagues-more">
          Browse all ›
        </Link>
      </div>
      <ul className="joinable-leagues-list">
        {visible.map((league) => (
          <li key={league.id} className="joinable-league-row">
            <div className="joinable-league-info">
              <span className="joinable-league-name">{league.name}</span>
              <span className="joinable-league-meta">
                <span aria-hidden="true">{SPORT_ICON[league.sport] ?? "🏆"}</span>{" "}
                {SPORT_LABEL[league.sport] ?? league.sport} {league.seasonYear} ·{" "}
                {league.memberCount} {league.memberCount === 1 ? "member" : "members"}
                {league.closesAtUtc ? (
                  <>
                    {" · "}
                    <JoinClosesLabel closesAtUtc={league.closesAtUtc} isJoinable={league.isJoinable} />
                  </>
                ) : null}
              </span>
            </div>
            <button
              type="button"
              className="joinable-league-join"
              onClick={() => setConfirming(league)}
            >
              Join
            </button>
          </li>
        ))}
      </ul>
      <JoinLeagueConfirmDialog
        league={confirming}
        onCancel={() => setConfirming(null)}
        onConfirm={() =>
          confirming && navigate(`/app/join/${confirming.id.replace(/-/g, "")}`)
        }
      />
    </section>
  );
}

export default JoinableLeaguesCard;
