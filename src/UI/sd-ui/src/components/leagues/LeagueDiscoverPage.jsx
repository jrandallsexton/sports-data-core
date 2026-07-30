import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import LeaguesApi from "api/leagues/leaguesApi";
import JoinClosesLabel from "./JoinClosesLabel";
import JoinLeagueConfirmDialog from "./JoinLeagueConfirmDialog";
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

// PublicLeagueDto.pickType is the BE enum's int value; same abbreviations the
// create page uses (SU / ATS / O-U).
const PICK_TYPE_LABEL = {
  1: "SU",
  2: "ATS",
  3: "O/U",
};

function LeagueDiscoverPage() {
  const navigate = useNavigate();
  const [leagues, setLeagues] = useState([]);
  const [loading, setLoading] = useState(true);
  // The league awaiting join confirmation; null = dialog closed. Joining is
  // effectively irreversible from the user's seat (there is no self-serve
  // leave), so the details render on the join path, not behind a separate
  // Details affordance most users would skip.
  const [confirming, setConfirming] = useState(null);

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

  const confirmJoin = () => {
    if (!confirming) return;
    // Hands off to the same AutoJoinRedirect flow invite links use — one join
    // path, one set of error handling.
    navigate(`/app/join/${confirming.id.replace(/-/g, "")}`);
  };

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
            <div>Format</div>
            <div>Members</div>
            <div>Commissioner</div>
            <div>Joinable</div>
            <div>Action</div>
          </div>

          {leagues.map((league) => (
            <div key={league.id} className="table-row">
              <div className="league-name">
                {/* Deliberately NOT a link: the league-detail page assumes
                    "My Leagues" context (its back affordance returns there),
                    which is wrong for a non-member browsing. Details a
                    browser needs belong on this list and in the join
                    dialog. */}
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
              <div className="league-format">
                {PICK_TYPE_LABEL[league.pickType] ?? "—"}
                {league.useConfidencePoints ? " · Confidence" : ""}
              </div>
              <div className="league-members">
                {league.memberCount} {league.memberCount === 1 ? "member" : "members"}
              </div>
              <div className="commissioner-name">{league.commissioner}</div>
              <div className={`league-joinable ${league.isJoinable ? "" : "league-joinable--closed"}`}>
                {/* The BE filters unjoinable leagues out of this list; the
                    closed rendering below is defensive only (e.g. a league
                    expiring between fetch and render). */}
                <JoinClosesLabel closesAtUtc={league.closesAtUtc} isJoinable={league.isJoinable} />
              </div>
              <div className="join-action">
                {league.isJoinable ? (
                  <button
                    type="button"
                    className="join-button"
                    onClick={() => setConfirming(league)}
                  >
                    Join
                  </button>
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

      <JoinLeagueConfirmDialog
        league={confirming}
        onCancel={() => setConfirming(null)}
        onConfirm={confirmJoin}
      />
    </div>
  );
}

export default LeagueDiscoverPage;
