import { Link } from "react-router-dom";

/**
 * Tier 1 primary slot — fallback shown when the user has at least one
 * league but no sport they care about is currently in-season. Surfaces
 * per-sport countdowns so users can see how close each product-focus
 * sport is to kickoff and spin up leagues ahead of time.
 *
 * Kickoff dates are hard-coded constants; revisit when ESPN publishes
 * each sport's official schedule if the dates shift.
 *
 * Seasonal calendar reference: docs/post-login-landing-design.md.
 */

const SPORTS = [
  // NCAAFB opens first weekend of September (always). `sportEnum` is the
  // value LeagueCreatePage reads from ?sport= to preselect the sport tab.
  {
    key: "NCAAFB",
    label: "NCAAFB",
    kickoff: new Date(Date.UTC(2026, 8, 5)),
    sportEnum: "FootballNcaa",
  },
  // NFL opens the Thursday after Labor Day (Kickoff Thursday).
  {
    key: "NFL",
    label: "NFL",
    kickoff: new Date(Date.UTC(2026, 8, 10)),
    sportEnum: "FootballNfl",
  },
];

function daysUntil(targetUtc, nowMs = Date.now()) {
  const msPerDay = 1000 * 60 * 60 * 24;
  return Math.ceil((targetUtc.getTime() - nowMs) / msPerDay);
}

function sportPhrase(sport, nowMs) {
  const days = daysUntil(sport.kickoff, nowMs);
  if (days <= 0) return { status: "live", text: `${sport.label} is underway` };
  return { status: "upcoming", text: `${sport.label} in ${days} ${days === 1 ? "day" : "days"}` };
}

function PrimarySlotOffSeasonCountdown() {
  const nowMs = Date.now();
  const sportsWithPhrases = SPORTS.map((s) => ({ ...s, phrase: sportPhrase(s, nowMs) }));
  const allLive = sportsWithPhrases.every((s) => s.phrase.status === "live");

  // Headline strategy:
  //   - All sports kicked off → "Picks are live" mode, drive users to picks.
  //   - Any sport still upcoming → render each sport's phrase on its own
  //     line so both countdowns carry equal visual weight and read like a
  //     scoreboard rather than a comma-run-on.
  const headline = allLive
    ? "NCAAFB and NFL are underway — pick your week"
    : sportsWithPhrases.map((s) => (
        <span key={s.key} className="home-primary__headline-line">{s.phrase.text}</span>
      ));

  const body = allLive
    ? "Jump into your leagues and lock in your picks before the next kickoff."
    : "Spin up your 2026 pick'em league now so you're ready for Week 1.";

  // CTAs — per-sport when any sport is still upcoming, single collapsed
  // button when all sports are live. For a sport that's already live, the
  // CTA routes to unified picks (no need to dedup — the picks page shows
  // all leagues the user belongs to). For an upcoming sport, link to league
  // creation with ?sport= so LeagueCreatePage preselects the correct tab.
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
      return (
        <Link
          key={s.key}
          to={isLive ? "/app/picks" : `/app/league/create?sport=${s.sportEnum}`}
          className="home-primary__cta home-primary__cta--primary"
        >
          {isLive ? `Pick ${s.label} games` : `Create ${s.label} league`}
        </Link>
      );
    });
  };

  return (
    <div className="home-primary home-primary--countdown">
      <div className="home-primary__eyebrow">2026 Season</div>
      <h1 className="home-primary__headline">{headline}</h1>
      <p className="home-primary__body">{body}</p>
      <div className="home-primary__actions">
        {renderActions()}
      </div>
    </div>
  );
}

export default PrimarySlotOffSeasonCountdown;
