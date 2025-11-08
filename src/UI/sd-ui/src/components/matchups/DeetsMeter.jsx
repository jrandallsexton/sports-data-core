import React from 'react';
import './DeetsMeter.css';

const DeetsMeter = ({ predictions, homeFranchiseSeasonId, awayFranchiseSeasonId }) => {
  // Find the StraightUp and ATS predictions
  const straightUpPrediction = predictions?.find(p => p.predictionType === 'StraightUp');
  const atsPrediction = predictions?.find(p => p.predictionType === 'AgainstTheSpread');

  // Helper function to determine which team is predicted to win and the probability
  const getPredictionData = (prediction, type) => {
    if (!prediction) return null;

    const isHomeFavored = prediction.winnerFranchiseSeasonId === homeFranchiseSeasonId;
    const winProbability = prediction.winProbability;

    // The winProbability is for the winnerFranchiseSeasonId team
    // Calculate percentages for away (left) and home (right)
    const homePercentage = isHomeFavored ? Math.round(winProbability * 100) : Math.round((1 - winProbability) * 100);
    const awayPercentage = 100 - homePercentage;

    return {
      isHomeFavored,
      awayPercentage,
      homePercentage
    };
  };

  const straightUpData = getPredictionData(straightUpPrediction, 'StraightUp');
  const atsData = getPredictionData(atsPrediction, 'ATS');

  const renderMeter = (data, label) => {
    if (!data) return null;

    const { awayPercentage, homePercentage } = data;

    return (
      <div className="deetsmeter-row">
        <div className="deetsmeter-meter">
          <div className="meter-gradient" style={{
            background: `linear-gradient(to right, 
              var(--away-color, #444) 0%, 
              var(--away-color, #444) ${awayPercentage}%, 
              var(--home-color, #666) ${awayPercentage}%, 
              var(--home-color, #666) 100%)`
          }}>
            {/* Label */}
            <div className="meter-label">{label}</div>
            {/* Away percentage */}
            <div className="meter-percentage away-percentage">
              {awayPercentage}%
            </div>
            {/* Midpoint line */}
            <div className="meter-midline"></div>
            {/* Home percentage */}
            <div className="meter-percentage home-percentage">
              {homePercentage}%
            </div>
          </div>
        </div>
      </div>
    );
  };

  // If no predictions, don't render anything
  if (!straightUpData && !atsData) {
    return null;
  }

  return (
    <div className="deetsmeter">
      <div className="deetsmeter-header">deetsMeter™</div>
      <div className="deetsmeter-meters">
        {renderMeter(straightUpData, 'SU')}
        {renderMeter(atsData, 'ATS')}
      </div>
    </div>
  );
};

export default DeetsMeter;
