import React from 'react';
import './ConfidencePicker.css';

const ConfidencePicker = ({ totalGames, usedPoints, onSelect, onCancel, currentPoint }) => {
  // Create array from 1 to totalGames (or at least 16 if totalGames is small/undefined, but better to respect prop)
  const maxPoints = totalGames || 16;
  const points = Array.from({ length: maxPoints }, (_, i) => i + 1);

  return (
    <div className="confidence-picker-overlay" onClick={onCancel}>
      <div className="confidence-picker-content" onClick={e => e.stopPropagation()}>
        <div className="confidence-picker-header">
          <span>Select Confidence Points</span>
          <button className="close-btn" onClick={onCancel}>&times;</button>
        </div>
        <div className="confidence-picker-grid">
          {points.map(point => {
            const isUsed = usedPoints.includes(point) && point !== currentPoint;
            const isSelected = point === currentPoint;
            
            return (
              <button
                key={point}
                className={`confidence-point-btn ${isUsed ? 'used' : ''} ${isSelected ? 'selected' : ''}`}
                onClick={() => !isUsed && onSelect(point)}
                disabled={isUsed}
                title={isUsed ? "Already used on another pick" : `Assign ${point} points`}
              >
                {point}
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
};

export default ConfidencePicker;
