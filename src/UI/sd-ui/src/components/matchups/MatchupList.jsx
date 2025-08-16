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
      {matchups.map((matchup, index) => (
        <MatchupCard
          key={matchup.ContestId}
          matchup={matchup}
          userPick={userPicks[matchup.ContestId]}
          onPick={onPick}
          onViewInsight={onViewInsight}
          isInsightUnlocked={index === 0 || isSubscribed}
        />
      ))}
    </div>
  );
}

export default MatchupList;
