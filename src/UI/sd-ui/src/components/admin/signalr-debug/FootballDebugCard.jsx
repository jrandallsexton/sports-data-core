import { useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import apiWrapper from '../../../api/apiWrapper';
import { useContestUpdates } from '../../../contexts/ContestUpdatesContext';
import {
  FOOTBALL_DEBUG_CONTEST_ID,
  FOOTBALL_DEBUG_AWAY_ID,
  FOOTBALL_DEBUG_HOME_ID,
} from './debugContestIds';
import './FootballDebugCard.css';

const SPORT_OPTIONS = [
  { value: 'FootballNcaa', label: 'NCAAFB' },
  { value: 'FootballNfl', label: 'NFL' },
];

const PERIOD_OPTIONS = ['Q1', 'Q2', 'Q3', 'Q4', 'OT'];

/**
 * SignalR debug widget for football. Renders a stylized field with
 * yard-line markers, score, possession indicator, period+clock, and
 * the most-recent payload received from SignalR. Includes preset
 * buttons + raw form for publishing FootballContestStateChanged
 * events through the API admin endpoint.
 *
 * See docs/signalr-debug-harness-plan.md.
 */
export default function FootballDebugCard() {
  const { getContestUpdate } = useContestUpdates();
  const live = getContestUpdate(FOOTBALL_DEBUG_CONTEST_ID) || {};

  const [sport, setSport] = useState('FootballNcaa');
  const [period, setPeriod] = useState('Q1');
  const [clock, setClock] = useState('15:00');
  const [awayScore, setAwayScore] = useState(0);
  const [homeScore, setHomeScore] = useState(0);
  const [possession, setPossession] = useState(FOOTBALL_DEBUG_AWAY_ID);
  const [isScoringPlay, setIsScoringPlay] = useState(false);
  // Ball position 0–100 yards from the away (visitor) goal line.
  // Matches FootballContestStateChanged.BallOnYardLine on the wire.
  const [ballOnYardLine, setBallOnYardLine] = useState(25);
  const [busy, setBusy] = useState(false);

  const broadcast = async (payload) => {
    setBusy(true);
    try {
      await apiWrapper.Admin.broadcastFootballState({ sport, ...payload });
      toast.success('Football state broadcast');
    } catch (err) {
      toast.error(`Broadcast failed: ${err.message || 'unknown'}`);
    } finally {
      setBusy(false);
    }
  };

  // ContestPlayCompleted is sport-neutral — Producer publishes one
  // alongside FootballContestStateChanged on every new play. Fire one
  // here so the SignalR debug surface exercises that consumer path too.
  const firePlayCompleted = async () => {
    setBusy(true);
    try {
      await apiWrapper.Admin.broadcastContestPlayCompleted({
        sport,
        playDescription: `Mock play @ ${new Date().toLocaleTimeString()}`,
      });
      toast.success('Play completed broadcast');
    } catch (err) {
      toast.error(`Broadcast failed: ${err.message || 'unknown'}`);
    } finally {
      setBusy(false);
    }
  };

  // Preset payload builders. Each fills the form so the user can see
  // exactly what was sent, then broadcasts.
  const fireGameStart = () => {
    const p = {
      period: 'Q1', clock: '15:00',
      awayScore: 0, homeScore: 0,
      possessionFranchiseSeasonId: FOOTBALL_DEBUG_AWAY_ID,
      isScoringPlay: false,
      // Touchback after the opening kickoff puts the ball at the away 25.
      ballOnYardLine: 25,
    };
    setPeriod(p.period); setClock(p.clock);
    setAwayScore(p.awayScore); setHomeScore(p.homeScore);
    setPossession(p.possessionFranchiseSeasonId); setIsScoringPlay(p.isScoringPlay);
    setBallOnYardLine(p.ballOnYardLine);
    broadcast(p);
  };

  const fireFieldGoal = () => {
    const home = possession === FOOTBALL_DEBUG_HOME_ID;
    const p = {
      period, clock,
      awayScore: home ? awayScore : awayScore + 3,
      homeScore: home ? homeScore + 3 : homeScore,
      possessionFranchiseSeasonId: possession,
      isScoringPlay: true,
      ballOnYardLine,
    };
    setAwayScore(p.awayScore); setHomeScore(p.homeScore); setIsScoringPlay(true);
    broadcast(p);
  };

  const fireTouchdown = () => {
    const home = possession === FOOTBALL_DEBUG_HOME_ID;
    // Ball ends up in the defending team's end zone — yard 100 if away
    // scored (drove toward home goal line), yard 0 if home scored.
    const endYard = home ? 0 : 100;
    const p = {
      period, clock,
      awayScore: home ? awayScore : awayScore + 6,
      homeScore: home ? homeScore + 6 : homeScore,
      possessionFranchiseSeasonId: possession,
      isScoringPlay: true,
      ballOnYardLine: endYard,
    };
    setAwayScore(p.awayScore); setHomeScore(p.homeScore); setIsScoringPlay(true);
    setBallOnYardLine(endYard);
    broadcast(p);
  };

  const fireExtraPoint = (points) => {
    const home = possession === FOOTBALL_DEBUG_HOME_ID;
    const p = {
      period, clock,
      awayScore: home ? awayScore : awayScore + points,
      homeScore: home ? homeScore + points : homeScore,
      possessionFranchiseSeasonId: possession,
      isScoringPlay: true,
      ballOnYardLine,
    };
    setAwayScore(p.awayScore); setHomeScore(p.homeScore); setIsScoringPlay(true);
    broadcast(p);
  };

  const flipPossession = () => {
    const next = possession === FOOTBALL_DEBUG_AWAY_ID
      ? FOOTBALL_DEBUG_HOME_ID
      : FOOTBALL_DEBUG_AWAY_ID;
    setPossession(next);
    broadcast({
      period, clock, awayScore, homeScore,
      possessionFranchiseSeasonId: next,
      isScoringPlay: false,
      ballOnYardLine,
    });
  };

  const fireCustom = (e) => {
    e.preventDefault();
    broadcast({
      period, clock, awayScore, homeScore,
      possessionFranchiseSeasonId: possession,
      isScoringPlay,
      ballOnYardLine,
    });
  };

  // Visual state pulled from the live merged context (so what's drawn
  // is what came back through SignalR, not what's sitting in the form).
  const liveAway = live.awayScore ?? 0;
  const liveHome = live.homeScore ?? 0;
  const livePeriod = live.period ?? '—';
  const liveClock = live.clock ?? '—';
  const livePossession = live.possessionFranchiseSeasonId;
  const liveScoring = live.isScoringPlay;
  const liveBallYard = live.ballOnYardLine;
  const awayHasBall = livePossession === FOOTBALL_DEBUG_AWAY_ID;
  const homeHasBall = livePossession === FOOTBALL_DEBUG_HOME_ID;
  const ballVisible = typeof liveBallYard === 'number' && liveBallYard >= 0 && liveBallYard <= 100;

  // MOCK: visualize a 10-yard movement on every new SignalR tick. The
  // backend doesn't yet send the prior yard line, so we synthesize one
  // 10 yards "behind" the current position relative to possession's
  // direction of attack (away drives toward yard 100, home toward yard 0).
  // Replace once FootballContestStateChanged carries BallOnYardLineStart.
  const MOCK_PLAY_YARDS = 10;
  const [trail, setTrail] = useState(null); // { start, end } in yard%
  const lastTickRef = useRef(null);
  useEffect(() => {
    if (!ballVisible) return;
    // Dedupe key reflects only ball-specific state. Using
    // live.lastUpdated would refire the trail on unrelated context
    // bumps (e.g., ContestPlayCompleted) even when the ball didn't
    // move.
    const tickKey = `${liveBallYard}|${awayHasBall ? 'a' : 'h'}`;
    if (lastTickRef.current === tickKey) return;
    lastTickRef.current = tickKey;

    const drivingTowardHome = awayHasBall;
    const start = drivingTowardHome
      ? Math.max(0, liveBallYard - MOCK_PLAY_YARDS)
      : Math.min(100, liveBallYard + MOCK_PLAY_YARDS);
    setTrail({ start, end: liveBallYard });
  }, [liveBallYard, awayHasBall, ballVisible]);

  return (
    <div className="football-debug-card">
      <h3 className="football-debug-card__title">Football SignalR Debug</h3>

      <div className={`football-field ${liveScoring ? 'football-field--scoring' : ''}`}>
        <div className="football-field__endzone football-field__endzone--away">AWAY</div>
        <div className="football-field__playarea">
          {[10, 20, 30, 40, 50, 60, 70, 80, 90].map((pos, i) => {
            // Marker labels mirror around midfield: 10/20/30/40/50/40/30/20/10.
            const labelYard = pos <= 50 ? pos : 100 - pos;
            const s = String(labelYard);
            const left = s.slice(0, 1);
            const right = s.slice(1);
            return (
              <div
                key={pos}
                className="football-field__yardline"
                style={{ left: `${pos}%` }}
              >
                <span className="football-field__yardlabel">
                  <span className="football-field__yarddigit">{left}</span>
                  <span className="football-field__yarddigit">{right}</span>
                </span>
              </div>
            );
          })}
          {trail && (
            <div
              className={`football-field__trail ${trail.end >= trail.start ? 'football-field__trail--rightward' : 'football-field__trail--leftward'}`}
              style={{
                left: `${Math.min(trail.start, trail.end)}%`,
                width: `${Math.abs(trail.end - trail.start)}%`,
              }}
            />
          )}
          {ballVisible && (
            <span
              className="football-field__ball"
              style={{ left: `${liveBallYard}%` }}
              title={`Ball on yard ${liveBallYard}`}
            >
              🏈
            </span>
          )}
        </div>
        <div className="football-field__endzone football-field__endzone--home">HOME</div>
      </div>

      <div className="football-debug-card__scoreboard">
        <div className="football-debug-card__team">
          {awayHasBall && <span className="football-debug-card__possession">🏈</span>}
          <span className="football-debug-card__teamlabel">AWAY</span>
          <span className="football-debug-card__teamscore">{liveAway}</span>
        </div>
        <div className="football-debug-card__centerinfo">
          <div className="football-debug-card__period">{livePeriod}</div>
          <div className="football-debug-card__clock">{liveClock}</div>
          {liveScoring && <div className="football-debug-card__scoringflash">SCORING PLAY</div>}
        </div>
        <div className="football-debug-card__team">
          <span className="football-debug-card__teamscore">{liveHome}</span>
          <span className="football-debug-card__teamlabel">HOME</span>
          {homeHasBall && <span className="football-debug-card__possession">🏈</span>}
        </div>
      </div>

      <div className="football-debug-card__controls">
        <div className="football-debug-card__presets">
          <button type="button" disabled={busy} onClick={fireGameStart}>Game start</button>
          <button type="button" disabled={busy} onClick={fireFieldGoal}>Field goal (+3)</button>
          <button type="button" disabled={busy} onClick={fireTouchdown}>Touchdown (+6)</button>
          <button type="button" disabled={busy} onClick={() => fireExtraPoint(1)}>XP (+1)</button>
          <button type="button" disabled={busy} onClick={() => fireExtraPoint(2)}>2-pt (+2)</button>
          <button type="button" disabled={busy} onClick={flipPossession}>Flip possession</button>
          <button type="button" disabled={busy} onClick={firePlayCompleted}>Fire play log</button>
        </div>

        {live.lastPlayDescription && (
          <div className="football-debug-card__lastplay">
            <span className="football-debug-card__lastplaylabel">Last play:</span>
            <span className="football-debug-card__lastplaytext">{live.lastPlayDescription}</span>
          </div>
        )}

        <form className="football-debug-card__form" onSubmit={fireCustom}>
          <label>
            Sport
            <select value={sport} onChange={(e) => setSport(e.target.value)}>
              {SPORT_OPTIONS.map((s) => (
                <option key={s.value} value={s.value}>{s.label}</option>
              ))}
            </select>
          </label>
          <label>
            Period
            <select value={period} onChange={(e) => setPeriod(e.target.value)}>
              {PERIOD_OPTIONS.map((p) => (
                <option key={p} value={p}>{p}</option>
              ))}
            </select>
          </label>
          <label>
            Clock
            <input value={clock} onChange={(e) => setClock(e.target.value)} placeholder="MM:SS" />
          </label>
          <label>
            Away score
            <input type="number" value={awayScore} onChange={(e) => setAwayScore(parseInt(e.target.value, 10) || 0)} />
          </label>
          <label>
            Home score
            <input type="number" value={homeScore} onChange={(e) => setHomeScore(parseInt(e.target.value, 10) || 0)} />
          </label>
          <label>
            Possession
            <select value={possession} onChange={(e) => setPossession(e.target.value)}>
              <option value={FOOTBALL_DEBUG_AWAY_ID}>Away</option>
              <option value={FOOTBALL_DEBUG_HOME_ID}>Home</option>
            </select>
          </label>
          <label>
            Ball on yard (0–100)
            <input
              type="number"
              min="0"
              max="100"
              value={ballOnYardLine}
              onChange={(e) => {
                const n = parseInt(e.target.value, 10);
                if (Number.isNaN(n)) return setBallOnYardLine(0);
                setBallOnYardLine(Math.max(0, Math.min(100, n)));
              }}
            />
          </label>
          <label className="football-debug-card__inline">
            <input type="checkbox" checked={isScoringPlay} onChange={(e) => setIsScoringPlay(e.target.checked)} />
            Is scoring play
          </label>
          <button type="submit" disabled={busy} className="football-debug-card__submit">
            Broadcast custom
          </button>
        </form>
      </div>

      <details className="football-debug-card__rawpayload">
        <summary>Last received SignalR payload (raw)</summary>
        <pre>{JSON.stringify(live, null, 2)}</pre>
      </details>
    </div>
  );
}
