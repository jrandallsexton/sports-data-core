// src/components/matchups/MatchupList.jsx

import "./MatchupList.css";
import MatchupCard from "./MatchupCard";
import { FaSpinner } from "react-icons/fa";

function MatchupList({
  matchups,
  pickType,
  loading,
  userPicks,
  onPick,
  onViewInsight,
  isSubscribed,
  fadingOut = [],
  useConfidencePoints,
  usedConfidencePoints,
  totalGames
}) {
  if (loading) {
    return (
      <div style={{ textAlign: "center", marginTop: "40px" }}>
        <FaSpinner className="spinner" style={{ fontSize: "2rem" }} />
        Loading Matchups...
      </div>
    );
  }

  return (
    <div className="matchup-list">
      {matchups.map((matchup) => (
        <MatchupCard
          key={matchup.contestId}
          matchup={matchup}
          pickType={pickType}
          userPickFranchiseSeasonId={userPicks[matchup.contestId]?.franchiseId}
          userPickResult={userPicks[matchup.contestId]}
          onPick={onPick}
          onViewInsight={onViewInsight}
          isInsightUnlocked={true}
          isSubscribed={isSubscribed}
          isFadingOut={fadingOut.includes(matchup.contestId)}
          useConfidencePoints={useConfidencePoints}
          usedConfidencePoints={usedConfidencePoints}
          totalGames={totalGames}
        />
      ))}
    </div>
  );
}

export default MatchupList;
