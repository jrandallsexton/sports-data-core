// src/components/matchups/MatchupList.jsx

import MatchupCard from "./MatchupCard";
import { FaSpinner } from "react-icons/fa";

function MatchupList({ matchups, loading, userPicks, onPick, onViewInsight, isSubscribed }) {
  if (loading) {
    return (
      <div style={{ textAlign: "center", marginTop: "40px" }}>
        <FaSpinner className="spinner" style={{ fontSize: "2rem" }} />
        Loading Matchups...
      </div>
    );
  }

  return (
    <>
      {matchups.map((matchup, index) => (
        <MatchupCard
          key={matchup.id}
          matchup={matchup}
          userPick={userPicks[matchup.id]}
          onPick={onPick}
          onViewInsight={onViewInsight}
          isInsightUnlocked={index === 0 || isSubscribed}
        />
      ))}
    </>
  );
}

export default MatchupList;
