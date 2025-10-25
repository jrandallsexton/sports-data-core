import "./MatchupCard.css";
import { FaChartLine, FaLock, FaCheckCircle, FaTimes, FaClipboardList, FaSearchPlus, FaSearchMinus } from "react-icons/fa";
import { Bot } from 'lucide-react'
import { Link } from "react-router-dom";
import { formatToEasternTime } from "../../utils/timeUtils";
import { useState, useEffect } from "react";
import apiWrapper from "../../api/apiWrapper";
import TeamComparison from "../teams/TeamComparison";
import MiniSchedule from "./MiniSchedule";

function MatchupCard({
  matchup,
  userPickFranchiseSeasonId,
  userPickResult, // New: DTO containing isCorrect, franchiseId, etc.
  onPick,
  onViewInsight,
  isInsightUnlocked,
  isFadingOut = false
}) {
  // State for TeamComparison dialog
  const [showComparison, setShowComparison] = useState(false);
  const [comparisonLoading, setComparisonLoading] = useState(false);
  const [comparisonData, setComparisonData] = useState(null);
  const handleOpenComparison = async () => {
    setComparisonLoading(true);
    setShowComparison(true);
    try {
      const [awayRes, homeRes] = await Promise.all([
        apiWrapper.TeamCard.getStatistics(matchup.awaySlug, 2025, matchup.awayFranchiseSeasonId),
        apiWrapper.TeamCard.getStatistics(matchup.homeSlug, 2025, matchup.homeFranchiseSeasonId)
      ]);
      setComparisonData({
        teamA: {
          name: matchup.away,
          logoUri: matchup.awayLogoUri,
          stats: awayRes.data
        },
        teamB: {
          name: matchup.home,
          logoUri: matchup.homeLogoUri,
          stats: homeRes.data
        }
      });
    } catch (e) {
      setComparisonData(null);
    } finally {
      setComparisonLoading(false);
    }
  };
  const handleCloseComparison = () => {
    setShowComparison(false);
    setComparisonData(null);
  };
  // Use spreadCurrent and spreadOpen for spread display
  const homeSpread = matchup.spreadCurrent ?? 0;
  const homeSpreadOpen = matchup.spreadOpen;
  let spreadArrow = null;
  if (
    homeSpreadOpen !== undefined &&
    homeSpreadOpen !== null &&
    homeSpread !== homeSpreadOpen
  ) {
    const absCurrent = Math.abs(Number(homeSpread));
    const absOpen = Math.abs(Number(homeSpreadOpen));
    if (absCurrent < absOpen) {
      // Spread moved closer to zero (easier for home) - green down arrow
      spreadArrow = <span style={{ color: '#00c853', fontWeight: 700, marginRight: 2 }} title="Spread moved in favor of home">▼</span>;
    } else if (absCurrent > absOpen) {
      // Spread moved further from zero (harder for home) - red up arrow
      spreadArrow = <span style={{ color: '#ff1744', fontWeight: 700, marginRight: 2 }} title="Spread moved against home">▲</span>;
    }
  }
  const overUnder = (matchup.overUnderCurrent === null || matchup.overUnderCurrent === 0 || matchup.overUnderCurrent === 'TBD') ? 'Off' : matchup.overUnderCurrent;
  const overUnderOpen = matchup.overUnderOpen;
  let ouArrow = null;
  if (
    overUnderOpen !== undefined &&
    overUnderOpen !== null &&
    overUnder !== 'Off' &&
    overUnder !== overUnderOpen
  ) {
    if (Number(overUnder) > Number(overUnderOpen)) {
      // O/U moved up (raised) - red
      ouArrow = <span style={{ color: '#ff1744', fontWeight: 700, marginRight: 2 }} title="O/U moved up">▲</span>;
    } else if (Number(overUnder) < Number(overUnderOpen)) {
      // O/U moved down (lowered) - green
      ouArrow = <span style={{ color: '#00c853', fontWeight: 700, marginRight: 2 }} title="O/U moved down">▼</span>;
    }
  }
  const gameTime = formatToEasternTime(matchup.startDateUtc);
  const venue = matchup.venue ?? "TBD";
  const location = `${matchup.venueCity ?? ""}, ${matchup.venueState ?? ""}`;
  const seasonYear = matchup.seasonYear ?? 2025;

  // Determine pick result when game is complete
  const getUserPickResult = () => {
    if (!matchup.isComplete) return null;
    // Only use server-calculated result
    if (userPickResult && typeof userPickResult.isCorrect === 'boolean') {
      return userPickResult.isCorrect ? 'correct' : 'incorrect';
    }
    // If not scored, do not show any result
    return null;
  };

  const pickResult = getUserPickResult();

  // Local state for optimistic pick selection
  const [localPickFranchiseId, setLocalPickFranchiseId] = useState(null);

  // Reset local pick if matchup or user pick changes (e.g., after refresh or parent update)
  useEffect(() => {
    setLocalPickFranchiseId(null);
  }, [matchup.matchupId, userPickFranchiseSeasonId, userPickResult?.franchiseId]);

  // Determine selected team: prefer local pick, then userPickResult, then userPickFranchiseSeasonId
  const selectedFranchiseId =
    localPickFranchiseId ?? userPickResult?.franchiseId ?? userPickFranchiseSeasonId;

  const isAwaySelected = selectedFranchiseId === matchup.awayFranchiseSeasonId;
  const isHomeSelected = selectedFranchiseId === matchup.homeFranchiseSeasonId;

  const [now, setNow] = useState(new Date());

  useEffect(() => {
    const interval = setInterval(() => {
      setNow(new Date());
    }, 15000); // check every 15 seconds

    return () => clearInterval(interval); // cleanup on unmount
  }, []);

  // Picks are locked 5 minutes prior to kickoff
  const startTime = new Date(matchup.startDateUtc);
  const lockTime = new Date(startTime.getTime() - 5 * 60 * 1000); // subtract 5 minutes
  const isLocked = now > lockTime;

  const getCardBorderClass = () => {
    if (!matchup.isComplete) return ""; // No border for incomplete games
    
    // Check if user made a pick (either in new or old format)
    if (!userPickResult && !userPickFranchiseSeasonId) return "pick-no-submission"; // Red border for no pick
    
    return pickResult ? `pick-${pickResult}` : ""; // Green/red based on result
  };

  const [showAwayGames, setShowAwayGames] = useState(false);
  const [showHomeGames, setShowHomeGames] = useState(false);
  // State for real schedule data
  const [awaySchedule, setAwaySchedule] = useState([]);
  const [homeSchedule, setHomeSchedule] = useState([]);
  const [awayLoading, setAwayLoading] = useState(false);
  const [homeLoading, setHomeLoading] = useState(false);
  const [awayError, setAwayError] = useState(null);
  const [homeError, setHomeError] = useState(null);

  // Fetch last 5 games for away team
  useEffect(() => {
    if (!showAwayGames) return;
    setAwayLoading(true);
    setAwayError(null);
    apiWrapper.TeamCard.getBySlugAndSeason(matchup.awaySlug, seasonYear)
      .then(res => {
        setAwaySchedule(Array.isArray(res.data?.schedule) ? res.data.schedule : []);
      })
      .catch(() => setAwayError("Failed to load schedule"))
      .finally(() => setAwayLoading(false));
  }, [showAwayGames, matchup.awaySlug, seasonYear]);

  // Fetch last 5 games for home team
  useEffect(() => {
    if (!showHomeGames) return;
    setHomeLoading(true);
    setHomeError(null);
    apiWrapper.TeamCard.getBySlugAndSeason(matchup.homeSlug, seasonYear)
      .then(res => {
        setHomeSchedule(Array.isArray(res.data?.schedule) ? res.data.schedule : []);
      })
      .catch(() => setHomeError("Failed to load schedule"))
      .finally(() => setHomeLoading(false));
  }, [showHomeGames, matchup.homeSlug, seasonYear]);

  return (
    <div className={`matchup-card ${isFadingOut ? "fade-out" : ""} ${getCardBorderClass()}`}
         style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div className="matchup-card-content" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        {/* Away Team Row */}
        <div className="team-row">
          <div className="team-info">
            {matchup.awayLogoUri && (
              <img
                src={matchup.awayLogoUri}
                alt={`${matchup.away} logo`}
                className="matchup-logo"
              />
            )}
            <div className="team-details">
              <div className="team-name-row">
                {matchup.awayRank && (
                  <span className="team-ranking">#{matchup.awayRank}</span>
                )}
                <Link
                  to={`/app/sport/football/ncaa/team/${matchup.awaySlug}/${seasonYear}`}
                  className="team-link"
                >
                  {matchup.away}
                </Link>
              </div>
              <div className="team-record" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span>
                  {matchup.awayWins}-{matchup.awayLosses} ({matchup.awayConferenceWins}-{matchup.awayConferenceLosses})
                </span>
                <button
                  className="mini-schedule-icon-btn"
                  aria-label={showAwayGames ? "Hide last 5 games" : "Show last 5 games"}
                  onClick={() => setShowAwayGames(v => !v)}
                  style={{ marginLeft: 4 }}
                >
                  {showAwayGames ? (
                    <FaSearchMinus style={{ fontSize: '1.1em', verticalAlign: 'middle' }} aria-label="Hide last 5 games" />
                  ) : (
                    <FaSearchPlus style={{ fontSize: '1.1em', verticalAlign: 'middle' }} aria-label="Show last 5 games" />
                  )}
                </button>
              </div>
            </div>
          </div>
        </div>
        {showAwayGames && (
          awayLoading ? (
            <div style={{padding:4, fontSize:'0.95em'}}>Loading…</div>
          ) : awayError ? (
            <div style={{padding:4, color:'red', fontSize:'0.95em'}}>{awayError}</div>
          ) : (
            <MiniSchedule schedule={awaySchedule} seasonYear={seasonYear} />
          )
        )}

        {/* Home Team Row */}
        <div className="team-row">
          <div className="team-info">
            {matchup.homeLogoUri && (
              <img
                src={matchup.homeLogoUri}
                alt={`${matchup.home} logo`}
                className="matchup-logo"
              />
            )}
            <div className="team-details">
              <div className="team-name-row">
                {matchup.homeRank && (
                  <span className="team-ranking">#{matchup.homeRank}</span>
                )}
                <Link
                  to={`/app/sport/football/ncaa/team/${matchup.homeSlug}/${seasonYear}`}
                  className="team-link"
                >
                  {matchup.home}
                </Link>
              </div>
              <div className="team-record" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span>
                  {matchup.homeWins}-{matchup.homeLosses} ({matchup.homeConferenceWins}-{matchup.homeConferenceLosses})
                </span>
                <button
                  className="mini-schedule-icon-btn"
                  aria-label={showHomeGames ? "Hide last 5 games" : "Show last 5 games"}
                  onClick={() => setShowHomeGames(v => !v)}
                  style={{ marginLeft: 4 }}
                >
                  {showHomeGames ? (
                    <FaSearchMinus style={{ fontSize: '1.1em', verticalAlign: 'middle' }} aria-label="Hide last 5 games" />
                  ) : (
                    <FaSearchPlus style={{ fontSize: '1.1em', verticalAlign: 'middle' }} aria-label="Show last 5 games" />
                  )}
                </button>
              </div>
            </div>
          </div>
          <div className="team-spread">
            {homeSpread === 0 ? 'Off' : (
              <>
                {spreadArrow}
                {homeSpread > 0 ? `+${homeSpread}` : homeSpread}
              </>
            )}
            {homeSpreadOpen !== undefined && homeSpreadOpen !== null && homeSpreadOpen !== homeSpread && (
              <span style={{ color: '#adb5bd', fontSize: '0.95em', marginLeft: 6 }}>
                ({homeSpreadOpen > 0 ? `+${homeSpreadOpen}` : homeSpreadOpen})
              </span>
            )}
          </div>
        </div>
        {showHomeGames && (
          homeLoading ? (
            <div style={{padding:4, fontSize:'0.95em'}}>Loading…</div>
          ) : homeError ? (
            <div style={{padding:4, color:'red', fontSize:'0.95em'}}>{homeError}</div>
          ) : (
            <MiniSchedule schedule={homeSchedule} seasonYear={seasonYear} />
          )
        )}

        {/* Game Result - show final score if game is complete */}
        {matchup.isComplete ? (
          <div className="game-result">
            <div className="final-score">
              <span className="result-label">FINAL:</span>
              <span className="score-display">
                {matchup.awayShort} {matchup.awayScore} - {matchup.homeScore} {matchup.homeShort}
              </span>
            </div>
          </div>
        ) : (
          <div className="game-time-location">
            <div>{gameTime} | {matchup.broadcasts}</div>
            <div>{venue} | {location}</div>
          </div>
        )}

        <div className="spread-ou">
          O/U: {ouArrow}{overUnder}
          {overUnderOpen !== undefined && overUnderOpen !== null && overUnderOpen !== overUnder && (
            <span style={{ color: '#adb5bd', fontSize: '0.95em', marginLeft: 6 }}>
              ({overUnderOpen})
            </span>
          )}
        </div>
      </div>

      <div className="pick-buttons-row" style={{ marginTop: 'auto' }}>
        <div className="pick-buttons">
          <button
            className={`pick-button ${isAwaySelected ? "selected" : ""} ${
              pickResult && isAwaySelected ? `result-${pickResult}` : ""
            }`}
            onClick={() => {
              setLocalPickFranchiseId(matchup.awayFranchiseSeasonId);
              onPick(matchup, matchup.awayFranchiseSeasonId);
            }}
            disabled={isLocked}
          >
            {/* Always show checkmark if selected, unless game is complete and incorrect */}
            {isAwaySelected && (!pickResult || pickResult === 'correct') && <FaCheckCircle className="pick-result-icon" />}
            {pickResult && isAwaySelected && pickResult === 'incorrect' && <FaTimes className="pick-result-icon" />}
            {!pickResult && !isAwaySelected && isLocked && <FaLock className="pick-lock-icon" />}
            {matchup.awayShort}
            {matchup.aiWinnerFranchiseSeasonId === matchup.awayFranchiseSeasonId && (
              <span title="AI Selection" aria-label="AI Selection">
                <Bot className="ai-pick-indicator" style={{ marginLeft: 6, verticalAlign: 'middle' }} />
              </span>
            )}
          </button>
          {/* Team Comparison Button - now immediately before insight button */}
          <button
            className="comparison-button"
            onClick={handleOpenComparison}
            title="Compare Team Stats"
            style={{ marginLeft: 6, marginRight: 6 }}
            disabled={comparisonLoading}
          >
            <FaClipboardList />
          </button>

          <button
            className={`insight-button${matchup.isPreviewReviewed ? " insight-reviewed" : ""}`}
            onClick={() => onViewInsight(matchup)}
            disabled={
              !matchup.isPreviewAvailable || !isInsightUnlocked
            }
            title={
              !matchup.isPreviewAvailable
                ? "Preview not available"
                : isInsightUnlocked
                  ? (matchup.isPreviewReviewed ? "View Validated Insight" : "View Insight")
                  : "Unlock Insights with Subscription"
            }
            style={{ marginLeft: 6 }}
          >
            {isInsightUnlocked ? <FaChartLine /> : <FaLock />}
          </button>

          <button
            className={`pick-button ${isHomeSelected ? "selected" : ""} ${
              pickResult && isHomeSelected ? `result-${pickResult}` : ""
            }`}
            onClick={() => {
              setLocalPickFranchiseId(matchup.homeFranchiseSeasonId);
              onPick(matchup, matchup.homeFranchiseSeasonId);
            }}
            disabled={isLocked}
          >
            {/* Always show checkmark if selected, unless game is complete and incorrect */}
            {isHomeSelected && (!pickResult || pickResult === 'correct') && <FaCheckCircle className="pick-result-icon" />}
            {pickResult && isHomeSelected && pickResult === 'incorrect' && <FaTimes className="pick-result-icon" />}
            {!pickResult && !isHomeSelected && isLocked && <FaLock className="pick-lock-icon" />}
            {matchup.homeShort}
            {matchup.aiWinnerFranchiseSeasonId === matchup.homeFranchiseSeasonId && (
              <span title="AI Selection" aria-label="AI Selection">
                <Bot className="ai-pick-indicator" style={{ marginLeft: 6, verticalAlign: 'middle' }} />
              </span>
            )}
          </button>
        </div>
      </div>
    {/* TeamComparison Dialog */}
    {showComparison && (
      comparisonLoading || !comparisonData || !comparisonData.teamA || !comparisonData.teamB || !comparisonData.teamA.stats || !comparisonData.teamB.stats ? (
        <div className="team-comparison-dialog-backdrop">
          <div className="team-comparison-dialog" style={{textAlign: 'center', padding: '2rem'}}>
            Loading team comparison...
          </div>
        </div>
      ) : (
        <TeamComparison
          open={showComparison}
          onClose={handleCloseComparison}
          teamA={comparisonData.teamA}
          teamB={comparisonData.teamB}
          teamAColor={matchup.awayColor}
          teamBColor={matchup.homeColor}
        />
      )
    )}
    </div>
  );
}

export default MatchupCard;
