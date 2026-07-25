import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import SeasonApi from "../../api/seasonApi";
import {
  getLeagueCreationGates,
  formatGateDate,
} from "../../utils/leagueCreationGates";

/**
 * Tier 1 primary slot — fallback shown when the user has at least one
 * league but no sport they care about is currently in-season. Surfaces
 * per-sport countdowns so users can see how close each product-focus
 * sport is to kickoff and spin up leagues ahead of time.
 *
 * Kickoff dates are data-driven: each sport's kickoff is the StartDate of its
 * current season's Regular Season phase (TypeCode 2), fetched from
 * /api/{sport}/{league}/seasons/current. No hardcoded or rule-derived dates —
 * those were wrong (NCAAFB has no fixed rule; NFL's "Thursday after Labor Day"
 * isn't honored). See docs/features/data-driven-season-countdown.md.
 *
 * Seasonal calendar reference: docs/post-login-landing-design.md.
 */

const REGULAR_SEASON_TYPE_CODE = 2;

const SPORTS = [
  // `sportEnum` is the value LeagueCreatePage reads from ?sport= to preselect
  // the sport tab. `sport`/`league` are the API route segments.
  { key: "NCAAFB", label: "NCAAFB", sportEnum: "FootballNcaa", sport: "football", league: "ncaa" },
  { key: "NFL", label: "NFL", sportEnum: "FootballNfl", sport: "football", league: "nfl" },
];

function daysUntil(kickoffIso, nowMs) {
  const msPerDay = 1000 * 60 * 60 * 24;
  return Math.ceil((new Date(kickoffIso).getTime() - nowMs) / msPerDay);
}

// Kickoff = the Regular Season phase's start. null when the season (or that
// phase) isn't sourced yet — the caller renders "kickoff coming soon" rather
// than a wrong countdown.
function regularSeasonStart(currentSeason) {
  const phase = currentSeason?.phases?.find(
    (p) => p.typeCode === REGULAR_SEASON_TYPE_CODE
  );
  return phase?.startDate ?? null;
}

function sportPhrase(sport, nowMs) {
  if (!sport.kickoff) {
    return { status: "unknown", text: `${sport.label} kickoff coming soon` };
  }
  const days = daysUntil(sport.kickoff, nowMs);
  if (days <= 0) return { status: "live", text: `${sport.label} is underway` };
  return {
    status: "upcoming",
    text: `${sport.label} in ${days} ${days === 1 ? "day" : "days"}`,
  };
}

function PrimarySlotOffSeasonCountdown() {
  // { NCAAFB: { kickoff, seasonYear } | null, ... } once loaded.
  const [seasons, setSeasons] = useState(null);
  // Active creation gates keyed by backend Sport enum: { FootballNcaa: opensUtc }.
  // A sport present is locked (create CTA becomes "opens {date}"). See
  // docs/features/league-creation-availability-gate.md.
  const [gates, setGates] = useState({});

  useEffect(() => {
    let cancelled = false;

    Promise.all(
      SPORTS.map((s) =>
        SeasonApi.getCurrentSeason(s.sport, s.league)
          .then((res) => ({
            key: s.key,
            kickoff: regularSeasonStart(res.data),
            seasonYear: res.data?.seasonYear ?? null,
          }))
          // A sport with no sourced season (404) is a valid state, not an error
          // — surface it as "coming soon" rather than failing the whole slot.
          .catch(() => ({ key: s.key, kickoff: null, seasonYear: null }))
      )
    ).then((results) => {
      if (cancelled) return;
      setSeasons(
        Object.fromEntries(results.map((r) => [r.key, r]))
      );
    });

    getLeagueCreationGates().then((g) => {
      if (!cancelled) setGates(g);
    });

    return () => {
      cancelled = true;
    };
  }, []);

  if (seasons === null) {
    return (
      <div className="home-primary home-primary--countdown">
        <p className="home-primary__body">Loading the season schedule…</p>
      </div>
    );
  }

  const nowMs = Date.now();
  const sportsWithPhrases = SPORTS.map((s) => ({
    ...s,
    kickoff: seasons[s.key]?.kickoff ?? null,
    seasonYear: seasons[s.key]?.seasonYear ?? null,
    phrase: sportPhrase({ ...s, kickoff: seasons[s.key]?.kickoff ?? null }, nowMs),
  }));

  const allLive = sportsWithPhrases.every((s) => s.phrase.status === "live");
  // Every surfaced sport is gated from creation → don't urge "spin up a league
  // now" when no CTA can act on it. (Live sports are never active gates, so this
  // implies none are live.)
  const allGated = sportsWithPhrases.every((s) => Boolean(gates[s.sportEnum]));

  // Eyebrow season year — first sport that reported one. Falls back to a
  // generic label if no season is sourced yet.
  const seasonYear =
    sportsWithPhrases.find((s) => s.seasonYear)?.seasonYear ?? null;

  // Headline strategy:
  //   - All sports kicked off → "Picks are live" mode, drive users to picks.
  //   - Any sport still upcoming (or coming soon) → render each sport's phrase
  //     on its own line so both carry equal visual weight and read like a
  //     scoreboard rather than a comma-run-on.
  const headline = allLive
    ? "NCAAFB and NFL are underway — pick your week"
    : sportsWithPhrases.map((s) => (
        <span key={s.key} className="home-primary__headline-line">
          {s.phrase.text}
        </span>
      ));

  const body = allLive
    ? "Jump into your leagues and lock in your picks before the next kickoff."
    : allGated
    ? "Leagues open soon — we'll be ready before Week 1."
    : seasonYear
    ? `Spin up your ${seasonYear} pick'em league now so you're ready for Week 1.`
    : "Spin up your pick'em league now so you're ready for Week 1.";

  // CTAs — per-sport when any sport is still upcoming, single collapsed
  // button when all sports are live. For a sport that's already live, the
  // CTA routes to unified picks (no need to dedup — the picks page shows
  // all leagues the user belongs to). Otherwise link to league creation with
  // ?sport= so LeagueCreatePage preselects the correct tab.
  const renderActions = () => {
    if (allLive) {
      return (
        <Link to="/app/picks" className="home-primary__cta home-primary__cta--primary">
          Go to picks
        </Link>
      );
    }

    return sportsWithPhrases.map((s) => {
      const isLive = s.phrase.status === "live";
      if (isLive) {
        return (
          <Link
            key={s.key}
            to="/app/picks"
            className="home-primary__cta home-primary__cta--primary"
          >
            {`Pick ${s.label} games`}
          </Link>
        );
      }

      // Creation gated (e.g. NCAAFB awaiting AP Poll release) — show when it
      // opens instead of a create link. The server enforces the same gate.
      const opensUtc = gates[s.sportEnum];
      if (opensUtc) {
        return (
          <span
            key={s.key}
            className="home-primary__cta home-primary__cta--disabled"
            aria-disabled="true"
            title={`${s.label} league creation opens ${formatGateDate(opensUtc)}`}
          >
            {`${s.label} leagues open ${formatGateDate(opensUtc)}`}
          </span>
        );
      }

      return (
        <Link
          key={s.key}
          to={`/app/league/create?sport=${s.sportEnum}`}
          className="home-primary__cta home-primary__cta--primary"
        >
          {`Create ${s.label} league`}
        </Link>
      );
    });
  };

  return (
    <div className="home-primary home-primary--countdown">
      <div className="home-primary__eyebrow">{seasonYear ? `${seasonYear} Season` : "Upcoming Season"}</div>
      <h1 className="home-primary__headline">{headline}</h1>
      <p className="home-primary__body">{body}</p>
      <div className="home-primary__actions">{renderActions()}</div>
    </div>
  );
}

export default PrimarySlotOffSeasonCountdown;
