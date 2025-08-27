// src/components/matchups/MatchupList.jsx

import "./MatchupList.css";
import MatchupCard from "./MatchupCard";
import { FaSpinner } from "react-icons/fa";

function MatchupList({
  matchups,
  loading,
  userPicks,
  onPick,
  onViewInsight,
  isSubscribed,
  fadingOut = []
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
          userPickFranchiseSeasonId={userPicks[matchup.contestId]}
          onPick={onPick}
          onViewInsight={onViewInsight}
          isInsightUnlocked={true}
          isSubscribed={isSubscribed}
          isFadingOut={fadingOut.includes(matchup.contestId)}
        />
      ))}
    </div>
  );
}

export default MatchupList;
