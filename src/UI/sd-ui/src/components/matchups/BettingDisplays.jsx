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

/**
 * SpreadAndOverUnderDisplay component - displays both spread and over/under together
 * @param {object} props
 * @param {number} props.spread - Current spread value
 * @param {number} props.spreadOpen - Opening spread value
 * @param {number|string} props.overUnder - Current O/U value
 * @param {number} props.overUnderOpen - Opening O/U value
 * @param {string} [props.providerName] - Sportsbook name (e.g. "ESPN BET");
 *   rendered as a small muted suffix when present. Hidden when both spread
 *   and O/U are "Off" — no values means no source to attribute.
 */
export function SpreadAndOverUnderDisplay({ spread, spreadOpen, overUnder, overUnderOpen, providerName }) {
  const spreadArrow = calculateSpreadArrow(spread, spreadOpen);
  const overUnderValue = (overUnder === null || overUnder === 0 || overUnder === 'TBD') ? 'Off' : overUnder;
  const ouArrow = calculateOverUnderArrow(overUnderValue, overUnderOpen);
  
  const spreadValue = (spread === null || spread === 0 || spread === 'TBD') ? 'Off' : spread;
  
  return (
    <div className="spread-ou">
      <span className="spread-display">
        {spreadValue === 'Off' ? 'Off' : (
          <>
            {spreadArrow}
            {spread > 0 ? `+${spread}` : spread}
          </>
        )}
        {spreadValue !== 'Off' && spreadOpen !== undefined && spreadOpen !== null && spreadOpen !== spread && (
          <span style={{ color: '#adb5bd', fontSize: '0.95em', marginLeft: 6 }}>
            ({spreadOpen > 0 ? `+${spreadOpen}` : spreadOpen})
          </span>
        )}
      </span>
      <span className="ou-separator"> | </span>
      <span className="ou-display">
        {ouArrow}{overUnderValue}
        {overUnderOpen !== undefined && overUnderOpen !== null && overUnderOpen !== overUnderValue && (
          <span style={{ color: '#adb5bd', fontSize: '0.95em', marginLeft: 6 }}>
            ({overUnderOpen})
          </span>
        )}
      </span>
      {providerName && (spreadValue !== 'Off' || overUnderValue !== 'Off') && (
        <span
          className="odds-provider"
          style={{
            color: '#adb5bd',
            fontSize: '0.7em',
            // .spread-ou is a CSS grid (1fr auto 1fr) — span all columns so
            // textAlign: center is relative to the full row width, not the
            // first 1fr track only.
            gridColumn: '1 / -1',
            textAlign: 'center',
            marginTop: 2,
          }}
        >
          {providerName}
        </span>
      )}
    </div>
  );
}
