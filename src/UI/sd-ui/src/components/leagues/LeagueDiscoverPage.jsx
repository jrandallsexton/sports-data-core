import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import LeaguesApi from "api/leagues/leaguesApi";
import "./LeagueDiscoverPage.css";

// PublicLeagueDto.sport enum name -> short display label.
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

// Closed leagues are returned (badged) rather than hidden, so a nearly-started
// league still advertises itself — see league-join-policy-and-discovery.md.
const closesLabel = (league) => {
  if (!league.isJoinable) return "Closed";
  if (!league.closesAtUtc) return "Open";
  const d = new Date(league.closesAtUtc);
  return Number.isNaN(d.getTime())
    ? "Open"
    : `Closes ${d.toLocaleDateString(undefined, { month: "short", day: "numeric" })} ${d.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" })}`;
};

function LeagueDiscoverPage() {
  const [leagues, setLeagues] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    LeaguesApi.getPublicLeagues()
      .then((data) => {
        setLeagues(data || []);
      })
      .catch((err) => {
        console.error("Failed to load public leagues:", err);
      })
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="league-discover-page">
      <h2>Discover Public Leagues</h2>

      {loading ? (
        <div className="loading-message">Loading leagues...</div>
      ) : leagues.length === 0 ? (
        <div className="no-leagues-message">No public leagues available right now.</div>
      ) : (
        <div className="leagues-table">
          <div className="table-header">
            <div>League Name</div>
            <div>Sport</div>
            <div>Members</div>
            <div>Commissioner</div>
            <div>Joinable</div>
            <div>Action</div>
          </div>

          {leagues.map((league) => (
            <div key={league.id} className="table-row">
              <div className="league-name">
                {league.name}
                {league.description ? (
                  <div className="league-description">{league.description}</div>
                ) : null}
              </div>
              <div className="league-sport">
                <span className="league-sport-icon" aria-hidden="true">
                  {SPORT_ICON[league.sport] ?? "🏆"}
                </span>{" "}
                {SPORT_LABEL[league.sport] ?? league.sport} {league.seasonYear}
              </div>
              <div className="league-members">
                {league.memberCount} {league.memberCount === 1 ? "member" : "members"}
              </div>
              <div className="commissioner-name">{league.commissioner}</div>
              <div className={`league-joinable ${league.isJoinable ? "" : "league-joinable--closed"}`}>
                {closesLabel(league)}
              </div>
              <div className="join-action">
                {league.isJoinable ? (
                  <Link
                    to={`/app/join/${league.id.replace(/-/g, "")}`}
                    className="join-button"
                  >
                    Join League
                  </Link>
                ) : (
                  // The BE join gate rejects closed leagues; don't offer a
                  // button that 400s.
                  <span className="join-button join-button--disabled" aria-disabled="true">
                    Closed
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default LeagueDiscoverPage;
