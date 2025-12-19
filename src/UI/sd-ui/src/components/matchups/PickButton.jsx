import { FaCheckCircle, FaTimes, FaLock } from "react-icons/fa";
import { Bot } from 'lucide-react';

/**
 * PickButton component - displays a team pick button with selection state
 * @param {object} props
 * @param {string} props.teamShort - Team short name
 * @param {boolean} props.isSelected - Whether this team is selected
 * @param {string} props.pickResult - Pick result ('correct', 'incorrect', or null)
 * @param {boolean} props.isLocked - Whether picks are locked
 * @param {function} props.onClick - Callback when button is clicked
 * @param {boolean} props.isAiPick - Whether this is the AI predicted winner
 * @param {boolean} props.isReadOnly - Whether user is in read-only mode
 * @param {number} props.confidencePoints - Confidence points assigned to this pick
 */
function PickButton({
  teamShort,
  isSelected,
  pickResult,
  isLocked,
  onClick,
  isAiPick,
  isReadOnly,
  confidencePoints
}) {

  return (
    <button
      className={`pick-button ${isSelected ? "selected" : ""} ${
        pickResult && isSelected ? `result-${pickResult}` : ""
      }`}
      onClick={onClick}
      disabled={isLocked}
      title={isReadOnly ? "Read-only mode" : ""}
    >
      {/* Always show checkmark if selected, unless game is complete and incorrect OR confidence points are used */}
      {isSelected && (!pickResult || pickResult === 'correct') && !confidencePoints && (
        <FaCheckCircle className="pick-result-icon" />
      )}
      {isSelected && confidencePoints && (
        <span className="confidence-badge" title={`${confidencePoints} Confidence Points`}>
          {confidencePoints}
        </span>
      )}
      {pickResult && isSelected && pickResult === 'incorrect' && (
        <FaTimes className="pick-result-icon" />
      )}
      {!pickResult && !isSelected && isLocked && (
        <FaLock className="pick-lock-icon" />
      )}
      {teamShort}
      {isAiPick && (
        <span title="AI Selection" aria-label="AI Selection">
          <Bot className="ai-pick-indicator" style={{ marginLeft: 6, verticalAlign: 'middle' }} />
        </span>
      )}
    </button>
  );
}

export default PickButton;
