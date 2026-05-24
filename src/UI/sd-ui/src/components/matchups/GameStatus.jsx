import { Link } from "react-router-dom";
import { Webcam } from "lucide-react";
import { contestLink } from '../../utils/sportLinks';
import FootballGameStatusInProgress from './FootballGameStatusInProgress';
import BaseballGameStatusInProgress from './BaseballGameStatusInProgress';

// Mid-game paused states. Game is technically still live (score + period/
// inning are meaningful), but no play is happening. Render the existing
// InProgress block beneath a banner that names the reason via
// statusDescription.toUpperCase().
const DELAY_STATUSES = new Set(['STATUS_DELAYED', 'STATUS_RAIN_DELAY', 'STATUS_SUSPENDED']);

// Terminal "game won't be played as scheduled" states. Both share the
// strikethrough-original-time visual; statusDescription drives the label.
const TERMINAL_STATUSES = new Set(['STATUS_POSTPONED', 'STATUS_CANCELED']);

/**
 * GameStatus - top-level dispatcher for the status block on a matchup
 * card. Branches on `status` (raw ESPN type name, e.g. "STATUS_FINAL").
 * Displays human labels via `statusDescription` (e.g. "Final") where the
 * label isn't hard-coded.
 *
 *   - 'STATUS_FINAL'        → shared score-line markup (sport-agnostic).
 *   - 'STATUS_IN_PROGRESS'  → dispatch to a per-sport child component
 *                             (FootballGameStatusInProgress /
 *                             BaseballGameStatusInProgress). The two
 *                             sports diverge meaningfully here — football
 *                             renders period, clock, score, and possession;
 *                             baseball renders score plus inning+count+outs,
 *                             runners, and a last-play line — so they own
 *                             their own JSX rather than one component piling
 *                             up sport-conditional branches.
 *   - DELAY_STATUSES        → Delayed / RainDelay / Suspended — game still
 *                             live, just paused. Same InProgress block
 *                             rendered beneath a delay banner using
 *                             statusDescription.toUpperCase().
 *   - TERMINAL_STATUSES     → Postponed / Canceled — label
 *                             (statusDescription.toUpperCase()) + struck-
 *                             through gameTime + venue.
 *   - default               → Scheduled / unknown: shared time+venue markup.
 *
 * Sport routing is keyed off `leagueSport` (the backend Sport enum
 * name, e.g. "BaseballMlb"). When omitted or unrecognized, falls back
 * to football rendering — preserves prior behavior on routes that
 * don't yet plumb the prop through.
 */
function GameStatus({
  status,
  statusDescription,
  awayShort,
  homeShort,
  awayScore,
  homeScore,
  gameTime,
  broadcasts,
  venue,
  location,
  period,
  clock,
  awayFranchiseSeasonId,
  homeFranchiseSeasonId,
  possessionFranchiseSeasonId,
  isScoringPlay,
  // Baseball-specific live fields (populated by ContestUpdatesContext
  // on BaseballPlayCompleted). Ignored on the football branch.
  inning,
  halfInning,
  balls,
  strikes,
  outs,
  runnerOnFirst,
  runnerOnSecond,
  runnerOnThird,
  lastPlayDescription,
  atBatShortName,
  atBatPositionAbbreviation,
  atBatHeadshotUrl,
  pitchingShortName,
  pitchingPositionAbbreviation,
  pitchingHeadshotUrl,
  awayLogoUri,
  homeLogoUri,
  contestId,
  leagueSport,
  sport,
  league,
  streamScheduledTimeUtc,
}) {
  if (status === 'STATUS_FINAL') {
    const scoreContent = (
      <>
        <span className="result-label">FINAL:</span>
        <span className="score-display">
          {awayShort} {awayScore} - {homeScore} {homeShort}
        </span>
      </>
    );

    return (
      <div className="game-result">
        <div className="final-score">
          {contestId ? (
            <Link
              to={contestLink(contestId, sport, league)}
              className="final-score-link"
              target="_blank"
              rel="noopener noreferrer"
            >
              {scoreContent}
            </Link>
          ) : (
            scoreContent
          )}
        </div>
      </div>
    );
  }

  const isDelayed = DELAY_STATUSES.has(status);
  if (status === 'STATUS_IN_PROGRESS' || status === 'STATUS_HALFTIME' || isDelayed) {
    const inProgressBlock = leagueSport === 'BaseballMlb' ? (
      <BaseballGameStatusInProgress
        awayShort={awayShort}
        homeShort={homeShort}
        awayScore={awayScore}
        homeScore={homeScore}
        inning={inning}
        halfInning={halfInning}
        balls={balls}
        strikes={strikes}
        outs={outs}
        runnerOnFirst={runnerOnFirst}
        runnerOnSecond={runnerOnSecond}
        runnerOnThird={runnerOnThird}
        lastPlayDescription={lastPlayDescription}
        atBatShortName={atBatShortName}
        atBatPositionAbbreviation={atBatPositionAbbreviation}
        atBatHeadshotUrl={atBatHeadshotUrl}
        pitchingShortName={pitchingShortName}
        pitchingPositionAbbreviation={pitchingPositionAbbreviation}
        pitchingHeadshotUrl={pitchingHeadshotUrl}
        awayLogoUri={awayLogoUri}
        homeLogoUri={homeLogoUri}
        isScoringPlay={isScoringPlay}
        contestId={contestId}
        sport={sport}
        league={league}
      />
    ) : (
      // Default: football rendering. Covers FootballNcaa / FootballNfl
      // explicitly and any unrecognized leagueSport (so callers that
      // don't yet thread the prop through don't regress).
      <FootballGameStatusInProgress
        awayShort={awayShort}
        homeShort={homeShort}
        awayScore={awayScore}
        homeScore={homeScore}
        period={period}
        clock={clock}
        awayFranchiseSeasonId={awayFranchiseSeasonId}
        homeFranchiseSeasonId={homeFranchiseSeasonId}
        possessionFranchiseSeasonId={possessionFranchiseSeasonId}
        isScoringPlay={isScoringPlay}
        lastPlayDescription={lastPlayDescription}
        contestId={contestId}
        sport={sport}
        league={league}
      />
    );

    if (isDelayed) {
      return (
        <>
          <div className="game-delay-banner">
            {(statusDescription || status || '').toUpperCase()}
          </div>
          {inProgressBlock}
        </>
      );
    }
    return inProgressBlock;
  }

  if (TERMINAL_STATUSES.has(status)) {
    return (
      <div className="game-time-location game-time-location-postponed">
        <div className="result-label">{(statusDescription || status || '').toUpperCase()}</div>
        <div className="game-time-original">{gameTime}</div>
        <div>{venue} | {location}</div>
      </div>
    );
  }

  // Scheduled or other status
  return (
    <>
      <div className="game-time-location">
        <div>{gameTime}</div>
        {broadcasts && <div>{broadcasts}</div>}
        <div>{venue} | {location}</div>
      </div>
      {streamScheduledTimeUtc && contestId && (
        <div className="game-result game-result-stream">
          <Link
            to={contestLink(contestId, sport, league)}
            className="final-score-link"
            target="_blank"
            rel="noopener noreferrer"
            aria-label={`View live stream: ${awayShort ?? 'Away'} at ${homeShort ?? 'Home'}${gameTime ? `, ${gameTime}` : ''}`}
          >
            <Webcam size={16} aria-hidden="true" />
            <span style={{ marginLeft: 6 }}>View</span>
          </Link>
        </div>
      )}
    </>
  );
}

export default GameStatus;
