import { calculateSpreadArrow, calculateOverUnderArrow } from '../../utils/bettingUtils';

/**
 * SpreadDisplay component - displays betting spread with movement indicator
 * @param {object} props
 * @param {number} props.spread - Current spread value
 * @param {number} props.spreadOpen - Opening spread value
 */
export function SpreadDisplay({ spread, spreadOpen }) {
  const arrow = calculateSpreadArrow(spread, spreadOpen);
  
  return (
    <>
      {spread === 0 ? 'Off' : (
        <>
          {arrow}
          {spread > 0 ? `+${spread}` : spread}
        </>
      )}
      {spreadOpen !== undefined && spreadOpen !== null && spreadOpen !== spread && (
        <span style={{ color: '#adb5bd', fontSize: '0.95em', marginLeft: 6 }}>
          ({spreadOpen > 0 ? `+${spreadOpen}` : spreadOpen})
        </span>
      )}
    </>
  );
}

/**
 * OverUnderDisplay component - displays over/under line with movement indicator
 * @param {object} props
 * @param {number|string} props.overUnder - Current O/U value
 * @param {number} props.overUnderOpen - Opening O/U value
 */
export function OverUnderDisplay({ overUnder, overUnderOpen }) {
  const overUnderValue = (overUnder === null || overUnder === 0 || overUnder === 'TBD') ? 'Off' : overUnder;
  const arrow = calculateOverUnderArrow(overUnderValue, overUnderOpen);
  
  return (
    <div className="spread-ou">
      O/U: {arrow}{overUnderValue}
      {overUnderOpen !== undefined && overUnderOpen !== null && overUnderOpen !== overUnderValue && (
        <span style={{ color: '#adb5bd', fontSize: '0.95em', marginLeft: 6 }}>
          ({overUnderOpen})
        </span>
      )}
    </div>
  );
}
