import React from "react";
import "./CFPBracket.css";

/**
 * CFP Tournament Bracket Component
 * Displays the 12-team College Football Playoff bracket
 * 
 * Expected data structure:
 * bracket: {
 *   firstRound: [{ seed1, team1, seed2, team2, winner }],
 *   quarterfinals: [{ seed, team, opponent, winner }],
 *   semifinals: [{ team1, team2, winner }],
 *   championship: { team1, team2, winner, location, date }
 * }
 */
function CFPBracket({ bracket }) {
  if (!bracket) {
    return <div className="cfp-bracket-placeholder">Bracket data not available</div>;
  }

  const renderTeam = (team, seed) => {
    if (!team) {
      return <div className="bracket-team empty">TBD</div>;
    }

    return (
      <div className="bracket-team">
        <span className="team-seed">{seed}</span>
        {team.franchiseLogoUrl && (
          <img 
            src={team.franchiseLogoUrl} 
            alt={team.franchiseName}
            className="bracket-team-logo"
          />
        )}
        <span className="bracket-team-name">{team.franchiseName}</span>
      </div>
    );
  };

  return (
    <div className="cfp-bracket">
      {/* <h3>College Football Playoff Bracket</h3> */}
      
      <div className="bracket-container">
        {/* First Round */}
        <div className="bracket-round first-round">
          <div className="round-label">First Round</div>
          {bracket.firstRound?.map((matchup, idx) => (
            <div key={idx} className="bracket-matchup">
              {renderTeam(matchup.team1, matchup.seed1)}
              {renderTeam(matchup.team2, matchup.seed2)}
            </div>
          ))}
        </div>

        {/* Quarterfinals */}
        <div className="bracket-round quarterfinals">
          <div className="round-label">Quarterfinals</div>
          {bracket.quarterfinals?.map((matchup, idx) => (
            <div key={idx} className="bracket-matchup">
              {renderTeam(matchup.team1, matchup.seed1)}
              {renderTeam(matchup.team2, matchup.seed2)}
            </div>
          ))}
        </div>

        {/* Semifinals */}
        <div className="bracket-round semifinals">
          <div className="round-label">Semifinals</div>
          {bracket.semifinals?.map((matchup, idx) => (
            <div key={idx} className="bracket-matchup">
              {renderTeam(matchup.team1, matchup.seed1)}
              {renderTeam(matchup.team2, matchup.seed2)}
            </div>
          ))}
        </div>

        {/* Championship */}
        <div className="bracket-round championship">
          <div className="round-label">Championship</div>
          {bracket.championship && (
            <div className="bracket-matchup">
              {renderTeam(bracket.championship.team1, bracket.championship.seed1)}
              {renderTeam(bracket.championship.team2, bracket.championship.seed2)}
              {bracket.championship.location && (
                <div className="championship-info">
                  {bracket.championship.location}
                  {bracket.championship.date && ` - ${bracket.championship.date}`}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default CFPBracket;
