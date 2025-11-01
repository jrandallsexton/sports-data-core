import "./MatchupCard.css";
import { FaChartLine, FaLock, FaClipboardList } from "react-icons/fa";
import { formatToEasternTime } from "../../utils/timeUtils";
import { useState, useEffect } from "react";
import TeamComparison from "../teams/TeamComparison";
import { useUserDto } from "../../contexts/UserContext";
import { getPickResultClass } from "../../utils/bettingUtils";
import { useTeamSchedule } from "../../hooks/useTeamSchedule";
import { usePickLocking } from "../../hooks/usePickLocking";
import { useTeamComparison } from "../../hooks/useTeamComparison";
import TeamRow from "./TeamRow";
import GameStatus from "./GameStatus";
import PickButton from "./PickButton";
import { SpreadDisplay, OverUnderDisplay } from "./BettingDisplays";

function MatchupCard({
  matchup,
  userPickFranchiseSeasonId,
  userPickResult, // New: DTO containing isCorrect, franchiseId, etc.
  onPick,
  onViewInsight,
  isInsightUnlocked,
  isFadingOut = false
}) {
  const { userDto } = useUserDto();
  const seasonYear = matchup.seasonYear ?? 2025;

  // Use custom hooks
  const {
    showComparison,
    comparisonLoading,
    comparisonData,
    handleOpenComparison,
    handleCloseComparison
  } = useTeamComparison(matchup, seasonYear);

  const { isLocked } = usePickLocking(matchup.startDateUtc, userDto?.isReadOnly);

  const {
    showAwayGames,
    setShowAwayGames,
    showHomeGames,
    setShowHomeGames,
    awaySchedule,
    homeSchedule,
    awayLoading,
    homeLoading,
    awayError,
    homeError
  } = useTeamSchedule(matchup.awaySlug, matchup.homeSlug, seasonYear);

  // Game details
  const gameTime = formatToEasternTime(matchup.startDateUtc);
  const venue = matchup.venue ?? "TBD";
  const location = `${matchup.venueCity ?? ""}, ${matchup.venueState ?? ""}`;

  // Determine pick result when game is complete
  const getUserPickResult = () => {
    if (matchup.status !== 'Final') return null;
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

  const cardBorderClass = getPickResultClass(
    matchup.status,
    userPickResult,
    userPickFranchiseSeasonId,
    pickResult
  );

  return (
    <div className={`matchup-card ${isFadingOut ? "fade-out" : ""} ${cardBorderClass}`}
         style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div className="matchup-card-content" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        {/* Away Team Row */}
        <TeamRow
          teamName={matchup.away}
          teamSlug={matchup.awaySlug}
          logoUri={matchup.awayLogoUri}
          rank={matchup.awayRank}
          wins={matchup.awayWins}
          losses={matchup.awayLosses}
          confWins={matchup.awayConferenceWins}
          confLosses={matchup.awayConferenceLosses}
          seasonYear={seasonYear}
          showSchedule={showAwayGames}
          onToggleSchedule={() => setShowAwayGames(v => !v)}
          schedule={awaySchedule}
          loading={awayLoading}
          error={awayError}
        />

        {/* Home Team Row */}
        <TeamRow
          teamName={matchup.home}
          teamSlug={matchup.homeSlug}
          logoUri={matchup.homeLogoUri}
          rank={matchup.homeRank}
          wins={matchup.homeWins}
          losses={matchup.homeLosses}
          confWins={matchup.homeConferenceWins}
          confLosses={matchup.homeConferenceLosses}
          seasonYear={seasonYear}
          showSchedule={showHomeGames}
          onToggleSchedule={() => setShowHomeGames(v => !v)}
          schedule={homeSchedule}
          loading={homeLoading}
          error={homeError}
          spreadDisplay={
            <SpreadDisplay
              spread={matchup.spreadCurrent}
              spreadOpen={matchup.spreadOpen}
            />
          }
        />

        {/* Game Status */}
        <GameStatus
          status={matchup.status}
          awayShort={matchup.awayShort}
          homeShort={matchup.homeShort}
          awayScore={matchup.awayScore}
          homeScore={matchup.homeScore}
          gameTime={gameTime}
          broadcasts={matchup.broadcasts}
          venue={venue}
          location={location}
        />

        <div className="spread-ou">
          <OverUnderDisplay
            overUnder={matchup.overUnderCurrent}
            overUnderOpen={matchup.overUnderOpen}
          />
        </div>
      </div>

      <div className="pick-buttons-row" style={{ marginTop: 'auto' }}>
        <div className="pick-buttons">
          <PickButton
            teamShort={matchup.awayShort}
            isSelected={isAwaySelected}
            pickResult={pickResult}
            isLocked={isLocked}
            isAiPick={matchup.aiWinnerFranchiseSeasonId === matchup.awayFranchiseSeasonId}
            isReadOnly={userDto?.isReadOnly}
            onClick={() => {
              setLocalPickFranchiseId(matchup.awayFranchiseSeasonId);
              onPick(matchup, matchup.awayFranchiseSeasonId);
            }}
          />

          {/* Team Comparison Button */}
          <button
            className="comparison-button"
            onClick={handleOpenComparison}
            title="Compare Team Stats"
            style={{ marginLeft: 6, marginRight: 6 }}
            disabled={comparisonLoading}
          >
            <FaClipboardList />
          </button>

          {/* Insight Button */}
          <button
            className={`insight-button${matchup.isPreviewReviewed ? " insight-reviewed" : ""}${
              !matchup.isPreviewAvailable && userDto?.isAdmin ? " insight-admin-missing" : ""
            }`}
            onClick={() => onViewInsight(matchup)}
            disabled={
              !matchup.isPreviewAvailable && !userDto?.isAdmin
                ? true
                : !isInsightUnlocked
            }
            title={
              !matchup.isPreviewAvailable && userDto?.isAdmin
                ? "Admin: Generate Preview"
                : !matchup.isPreviewAvailable
                  ? "Preview not available"
                  : isInsightUnlocked
                    ? (matchup.isPreviewReviewed ? "View Validated Insight" : "View Insight")
                    : "Unlock Insights with Subscription"
            }
            style={{ marginLeft: 6 }}
          >
            {isInsightUnlocked ? <FaChartLine /> : <FaLock />}
          </button>

          <PickButton
            teamShort={matchup.homeShort}
            isSelected={isHomeSelected}
            pickResult={pickResult}
            isLocked={isLocked}
            isAiPick={matchup.aiWinnerFranchiseSeasonId === matchup.homeFranchiseSeasonId}
            isReadOnly={userDto?.isReadOnly}
            onClick={() => {
              setLocalPickFranchiseId(matchup.homeFranchiseSeasonId);
              onPick(matchup, matchup.homeFranchiseSeasonId);
            }}
          />
        </div>
      </div>
    {/* TeamComparison Dialog */}
    {showComparison && (
      comparisonLoading || !comparisonData || !comparisonData.teamA ||!comparisonData.teamB ||
      !comparisonData.teamA.stats || !comparisonData.teamB.stats || !comparisonData.teamA.metrics || !comparisonData.teamB.metrics ? (
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
