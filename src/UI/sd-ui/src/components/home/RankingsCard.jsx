import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTheme } from "../../contexts/ThemeContext";
import apiWrapper from "../../api/apiWrapper";
import useCurrentSeasonYear from "../../hooks/useCurrentSeasonYear";
import { teamLink, rankingsLink } from "../../utils/sportLinks";
import "./RankingsCard.css";

/**
 * Tier 2 — compact Top 10 from the current AP poll (falls back to the
 * first poll returned when 'ap' is absent), linking to the full
 * /app/rankings page for all polls and all 25 entries.
 *
 * Renders null while loading, on error, and when no poll exists for the
 * current season — the home page never shows a broken or empty card.
 */
function RankingsCard() {
  const { theme } = useTheme();
  const { seasonYear, loading: seasonLoading } = useCurrentSeasonYear();
  const [poll, setPoll] = useState(null);

  useEffect(() => {
    if (seasonLoading || seasonYear === null) return;

    let cancelled = false;
    apiWrapper.Rankings.getSeasonRankings(seasonYear)
      .then((apiResult) => {
        if (cancelled) return;
        const data = apiResult?.data || apiResult;
        const polls = Array.isArray(data) ? data : [];
        // AP only on the home card; the fallback (AP absent) must never
        // surface CFP either — it's hidden app-wide until it publishes.
        const eligible = polls.filter((p) => p.pollId !== "cfp");
        setPoll(eligible.find((p) => p.pollId === "ap") || eligible[0] || null);
      })
      .catch(() => {
        // Self-nulling card: rankings being unavailable must not break Home.
      });

    return () => {
      cancelled = true;
    };
  }, [seasonLoading, seasonYear]);

  if (!poll?.entries?.length) return null;

  const top10 = poll.entries.slice(0, 10);

  return (
    <div className="rankings-card">
      <div className="rankings-card__header">
        <div className="rankings-card__eyebrow">
          {(poll.pollName || "RANKINGS").toUpperCase()}
        </div>
        <Link to={rankingsLink()} className="rankings-card__full">
          Full rankings ›
        </Link>
      </div>
      <ol className="rankings-card__list">
        {top10.map((team) => {
          const logoSrc =
            theme === "dark"
              ? team.franchiseLogoUrlDark || team.franchiseLogoUrlLight || team.franchiseLogoUrl
              : team.franchiseLogoUrlLight || team.franchiseLogoUrlDark || team.franchiseLogoUrl;
          return (
            <li key={team.franchiseSeasonId || team.rank} className="rankings-card__item">
              <Link
                to={teamLink(team.franchiseSlug || "", seasonYear)}
                className="rankings-card__row"
                aria-label={`Open ${team.franchiseName}`}
              >
                <span className="rankings-card__rank">{team.rank}</span>
                {/* Span always renders so rows without a logo keep column
                    alignment, matching YourLeaguesCard's icon handling. */}
                <span className="rankings-card__logo" aria-hidden="true">
                  {logoSrc ? <img src={logoSrc} alt="" /> : null}
                </span>
                <span className="rankings-card__name">
                  {team.franchiseName || "Unknown"}
                </span>
                <span className="rankings-card__record">
                  {team.wins}-{team.losses}
                </span>
              </Link>
            </li>
          );
        })}
      </ol>
    </div>
  );
}

export default RankingsCard;
