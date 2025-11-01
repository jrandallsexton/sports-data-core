/**
 * SpreadDisplay component - displays betting spread with movement indicator
 * @param {object} props
 * @param {number} props.spread - Current spread value
 * @param {number} props.spreadOpen - Opening spread value
 * @param {JSX.Element} props.arrow - Arrow indicator element
 */
export function SpreadDisplay({ spread, spreadOpen, arrow }) {
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
 * @param {JSX.Element} props.arrow - Arrow indicator element
 */
export function OverUnderDisplay({ overUnder, overUnderOpen, arrow }) {
  return (
    <div className="spread-ou">
      O/U: {arrow}{overUnder}
      {overUnderOpen !== undefined && overUnderOpen !== null && overUnderOpen !== overUnder && (
        <span style={{ color: '#adb5bd', fontSize: '0.95em', marginLeft: 6 }}>
          ({overUnderOpen})
        </span>
      )}
    </div>
  );
}
