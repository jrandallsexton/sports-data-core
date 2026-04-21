import { Link } from "react-router-dom";
import { FaSearchPlus, FaSearchMinus } from "react-icons/fa";
import { teamLink, resolveSportLeague } from '../../utils/sportLinks';
import MiniSchedule from "./MiniSchedule";

/**
 * TeamRow component - displays team information, record, and optional schedule
 * @param {object} props
 * @param {string} props.teamName - Full team name
 * @param {string} props.teamSlug - Team slug for routing
 * @param {number} props.rank - Team ranking (optional)
 * @param {string} props.logoUri - Team logo URL
 * @param {number} props.wins - Overall wins
 * @param {number} props.losses - Overall losses
 * @param {number} props.confWins - Conference wins
 * @param {number} props.confLosses - Conference losses
 * @param {number} props.seasonYear - Season year
 * @param {boolean} props.showSchedule - Whether schedule is expanded
 * @param {function} props.onToggleSchedule - Callback to toggle schedule
 * @param {array} props.schedule - Schedule data array
 * @param {boolean} props.loading - Schedule loading state
 * @param {string} props.error - Schedule error message
 */
function TeamRow({
  teamName,
  teamSlug,
  rank,
  logoUri,
  wins,
  losses,
  confWins,
  confLosses,
  seasonYear,
  leagueSport,
  showSchedule,
  onToggleSchedule,
  schedule,
  loading,
  error
}) {
  const { sport, league } = resolveSportLeague(leagueSport);
  return (
    <>
      <div className="team-row">
        <div className="team-info">
          {logoUri && (
            <img
              src={logoUri}
              alt={`${teamName} logo`}
              className="matchup-logo"
            />
          )}
          <div className="team-details">
            <div className="team-name-row">
              {rank && (
                <span className="team-ranking">#{rank}</span>
              )}
              <Link
                to={teamLink(teamSlug, seasonYear, sport, league)}
                className="team-link"
              >
                {teamName}
              </Link>
            </div>
            <div className="team-record-row">
              <div className="team-record">
                <span>
                  {wins}-{losses} ({confWins}-{confLosses})
                </span>
                <button
                  className="mini-schedule-icon-btn"
                  aria-label={showSchedule ? "Hide last 5 games" : "Show last 5 games"}
                  onClick={onToggleSchedule}
                  style={{ marginLeft: 4 }}
                >
                  {showSchedule ? (
                    <FaSearchMinus style={{ fontSize: '1.1em', verticalAlign: 'middle' }} aria-label="Hide last 5 games" />
                  ) : (
                    <FaSearchPlus style={{ fontSize: '1.1em', verticalAlign: 'middle' }} aria-label="Show last 5 games" />
                  )}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
      {showSchedule && (
        loading ? (
          <div style={{ padding: 4, fontSize: '0.95em' }}>Loading…</div>
        ) : error ? (
          <div style={{ padding: 4, color: 'red', fontSize: '0.95em' }}>{error}</div>
        ) : (
          <MiniSchedule schedule={schedule} seasonYear={seasonYear} leagueSport={leagueSport} />
        )
      )}
    </>
  );
}

export default TeamRow;
