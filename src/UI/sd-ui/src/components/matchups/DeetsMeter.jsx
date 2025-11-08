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
    const probability = prediction.winProbability;

    return {
      isHomeFavored,
      probability,
      displayPercentage: Math.round(probability * 100)
    };
  };

  const straightUpData = getPredictionData(straightUpPrediction, 'StraightUp');
  const atsData = getPredictionData(atsPrediction, 'ATS');

  const renderMeter = (data, label) => {
    if (!data) return null;

    const { isHomeFavored, probability, displayPercentage } = data;

    // Calculate gradient position (0-100%)
    // Away is on left, Home is on right
    const awayPercentage = isHomeFavored ? (100 - displayPercentage) : displayPercentage;
    const homePercentage = 100 - awayPercentage;

    return (
      <div className="deetsometer-row">
        <div className="deetsometer-meter">
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
    <div className="deetsometer">
      <div className="deetsometer-header">deetsMeter™</div>
      {renderMeter(straightUpData, 'SU')}
      {renderMeter(atsData, 'ATS')}
    </div>
  );
};

export default DeetsMeter;
