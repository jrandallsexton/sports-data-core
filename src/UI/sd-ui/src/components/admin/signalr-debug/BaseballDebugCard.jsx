import { useState } from 'react';
import toast from 'react-hot-toast';
import apiWrapper from '../../../api/apiWrapper';
import { useContestUpdates } from '../../../contexts/ContestUpdatesContext';
import { BASEBALL_DEBUG_CONTEST_ID } from './debugContestIds';
import './BaseballDebugCard.css';

const HALF_OPTIONS = ['Top', 'Bottom'];

/**
 * SignalR debug widget for baseball. Renders a stylized diamond with
 * bases lit up by RunnerOnFirst/Second/Third, score, inning header,
 * and current at-bat with count. Includes preset buttons + raw form
 * for publishing BaseballContestStateChanged events.
 *
 * See docs/signalr-debug-harness-plan.md.
 */
export default function BaseballDebugCard() {
  const { getContestUpdate } = useContestUpdates();
  const live = getContestUpdate(BASEBALL_DEBUG_CONTEST_ID) || {};

  const [inning, setInning] = useState(1);
  const [halfInning, setHalfInning] = useState('Top');
  const [awayScore, setAwayScore] = useState(0);
  const [homeScore, setHomeScore] = useState(0);
  const [balls, setBalls] = useState(0);
  const [strikes, setStrikes] = useState(0);
  const [outs, setOuts] = useState(0);
  const [first, setFirst] = useState(false);
  const [second, setSecond] = useState(false);
  const [third, setThird] = useState(false);
  const [busy, setBusy] = useState(false);

  const broadcast = async (payload) => {
    setBusy(true);
    try {
      await apiWrapper.Admin.broadcastBaseballState(payload);
      toast.success('Baseball state broadcast');
    } catch (err) {
      toast.error(`Broadcast failed: ${err.message || 'unknown'}`);
    } finally {
      setBusy(false);
    }
  };

  // ContestPlayCompleted is sport-neutral — fire one to exercise the
  // consumer path that lives outside the per-sport scoreboard tick.
  const firePlayCompleted = async () => {
    setBusy(true);
    try {
      await apiWrapper.Admin.broadcastContestPlayCompleted({
        sport: 'BaseballMlb',
        playDescription: `Mock pitch @ ${new Date().toLocaleTimeString()}`,
      });
      toast.success('Play completed broadcast');
    } catch (err) {
      toast.error(`Broadcast failed: ${err.message || 'unknown'}`);
    } finally {
      setBusy(false);
    }
  };

  // Always send the current full snapshot so the merge in
  // ContestUpdatesContext re-renders the diamond consistently.
  const snapshot = (overrides = {}) => ({
    inning, halfInning,
    awayScore, homeScore,
    balls, strikes, outs,
    runnerOnFirst: first,
    runnerOnSecond: second,
    runnerOnThird: third,
    atBatAthleteId: null,
    pitchingAthleteId: null,
    ...overrides,
  });

  const firePitch = (kind) => {
    const next = kind === 'ball'
      ? { balls: Math.min(balls + 1, 4) }
      : { strikes: Math.min(strikes + 1, 3) };
    if (kind === 'ball' && next.balls === 4) {
      // Walk: clear count, runner advances simplistically (push to first).
      setBalls(0); setStrikes(0); setFirst(true);
      broadcast(snapshot({ balls: 0, strikes: 0, runnerOnFirst: true }));
      return;
    }
    if (kind === 'strike' && next.strikes === 3) {
      // Strikeout: increment outs, clear count.
      const newOuts = Math.min(outs + 1, 3);
      setOuts(newOuts); setBalls(0); setStrikes(0);
      broadcast(snapshot({ outs: newOuts, balls: 0, strikes: 0 }));
      return;
    }
    if (kind === 'ball') setBalls(next.balls); else setStrikes(next.strikes);
    broadcast(snapshot(next));
  };

  const fireOut = () => {
    const newOuts = Math.min(outs + 1, 3);
    setOuts(newOuts); setBalls(0); setStrikes(0);
    broadcast(snapshot({ outs: newOuts, balls: 0, strikes: 0 }));
  };

  const fireSingle = () => {
    setFirst(true); setBalls(0); setStrikes(0);
    broadcast(snapshot({ runnerOnFirst: true, balls: 0, strikes: 0 }));
  };

  const fireHomeRun = () => {
    const runs = 1 + (first ? 1 : 0) + (second ? 1 : 0) + (third ? 1 : 0);
    const isTop = halfInning === 'Top';
    const newAway = isTop ? awayScore + runs : awayScore;
    const newHome = isTop ? homeScore : homeScore + runs;
    setAwayScore(newAway); setHomeScore(newHome);
    setFirst(false); setSecond(false); setThird(false);
    setBalls(0); setStrikes(0);
    broadcast(snapshot({
      awayScore: newAway, homeScore: newHome,
      runnerOnFirst: false, runnerOnSecond: false, runnerOnThird: false,
      balls: 0, strikes: 0,
    }));
  };

  const fireInningChange = () => {
    const nextHalf = halfInning === 'Top' ? 'Bottom' : 'Top';
    const nextInning = nextHalf === 'Top' ? inning + 1 : inning;
    setHalfInning(nextHalf); setInning(nextInning);
    setBalls(0); setStrikes(0); setOuts(0);
    setFirst(false); setSecond(false); setThird(false);
    broadcast(snapshot({
      halfInning: nextHalf, inning: nextInning,
      balls: 0, strikes: 0, outs: 0,
      runnerOnFirst: false, runnerOnSecond: false, runnerOnThird: false,
    }));
  };

  const fireCustom = (e) => {
    e.preventDefault();
    broadcast(snapshot());
  };

  // Visual state pulled from live merged context.
  const liveAway = live.awayScore ?? 0;
  const liveHome = live.homeScore ?? 0;
  const liveInning = live.inning ?? '—';
  const liveHalf = live.halfInning ?? '—';
  const liveBalls = live.balls ?? 0;
  const liveStrikes = live.strikes ?? 0;
  const liveOuts = live.outs ?? 0;
  const liveFirst = !!live.runnerOnFirst;
  const liveSecond = !!live.runnerOnSecond;
  const liveThird = !!live.runnerOnThird;

  return (
    <div className="baseball-debug-card">
      <h3 className="baseball-debug-card__title">Baseball SignalR Debug</h3>

      <div className="baseball-debug-card__field">
        <svg viewBox="0 0 200 200" className="baseball-diamond">
          {/* Outfield arc */}
          <path d="M 20 110 A 80 80 0 0 1 180 110 L 100 180 Z"
                fill="#2a8645" stroke="#ffffff44" strokeWidth="1" />
          {/* Infield diamond */}
          <polygon points="100,40 160,100 100,160 40,100"
                   fill="#a0673a" stroke="#fff" strokeWidth="1.5" />
          {/* Bases */}
          <rect x="92" y="32" width="16" height="16"
                transform="rotate(45 100 40)"
                className={`baseball-base ${liveSecond ? 'baseball-base--occupied' : ''}`} />
          <rect x="152" y="92" width="16" height="16"
                transform="rotate(45 160 100)"
                className={`baseball-base ${liveFirst ? 'baseball-base--occupied' : ''}`} />
          <rect x="32" y="92" width="16" height="16"
                transform="rotate(45 40 100)"
                className={`baseball-base ${liveThird ? 'baseball-base--occupied' : ''}`} />
          {/* Home plate */}
          <polygon points="100,150 110,160 105,170 95,170 90,160"
                   fill="#fff" stroke="#000" strokeWidth="1" />
        </svg>
      </div>

      <div className="baseball-debug-card__scoreboard">
        <div className="baseball-debug-card__team">
          <span className="baseball-debug-card__teamlabel">AWAY</span>
          <span className="baseball-debug-card__teamscore">{liveAway}</span>
        </div>
        <div className="baseball-debug-card__centerinfo">
          <div className="baseball-debug-card__inning">{liveHalf} {liveInning}</div>
          <div className="baseball-debug-card__count">
            {liveBalls}-{liveStrikes}, {liveOuts} out
          </div>
        </div>
        <div className="baseball-debug-card__team">
          <span className="baseball-debug-card__teamscore">{liveHome}</span>
          <span className="baseball-debug-card__teamlabel">HOME</span>
        </div>
      </div>

      <div className="baseball-debug-card__controls">
        <div className="baseball-debug-card__presets">
          <button type="button" disabled={busy} onClick={() => firePitch('ball')}>Ball</button>
          <button type="button" disabled={busy} onClick={() => firePitch('strike')}>Strike</button>
          <button type="button" disabled={busy} onClick={fireOut}>Out</button>
          <button type="button" disabled={busy} onClick={fireSingle}>Single</button>
          <button type="button" disabled={busy} onClick={fireHomeRun}>Home run</button>
          <button type="button" disabled={busy} onClick={fireInningChange}>Inning change</button>
          <button type="button" disabled={busy} onClick={firePlayCompleted}>Fire play log</button>
        </div>

        {live.lastPlayDescription && (
          <div className="baseball-debug-card__lastplay">
            <span className="baseball-debug-card__lastplaylabel">Last play:</span>
            <span className="baseball-debug-card__lastplaytext">{live.lastPlayDescription}</span>
          </div>
        )}

        <form className="baseball-debug-card__form" onSubmit={fireCustom}>
          <label>
            Inning
            <input type="number" min="1" value={inning}
                   onChange={(e) => setInning(parseInt(e.target.value, 10) || 1)} />
          </label>
          <label>
            Half
            <select value={halfInning} onChange={(e) => setHalfInning(e.target.value)}>
              {HALF_OPTIONS.map((h) => <option key={h} value={h}>{h}</option>)}
            </select>
          </label>
          <label>
            Away score
            <input type="number" value={awayScore}
                   onChange={(e) => setAwayScore(parseInt(e.target.value, 10) || 0)} />
          </label>
          <label>
            Home score
            <input type="number" value={homeScore}
                   onChange={(e) => setHomeScore(parseInt(e.target.value, 10) || 0)} />
          </label>
          <label>
            Balls
            <input type="number" min="0" max="4" value={balls}
                   onChange={(e) => setBalls(parseInt(e.target.value, 10) || 0)} />
          </label>
          <label>
            Strikes
            <input type="number" min="0" max="3" value={strikes}
                   onChange={(e) => setStrikes(parseInt(e.target.value, 10) || 0)} />
          </label>
          <label>
            Outs
            <input type="number" min="0" max="3" value={outs}
                   onChange={(e) => setOuts(parseInt(e.target.value, 10) || 0)} />
          </label>
          <label className="baseball-debug-card__inline">
            <input type="checkbox" checked={first} onChange={(e) => setFirst(e.target.checked)} />
            On 1st
          </label>
          <label className="baseball-debug-card__inline">
            <input type="checkbox" checked={second} onChange={(e) => setSecond(e.target.checked)} />
            On 2nd
          </label>
          <label className="baseball-debug-card__inline">
            <input type="checkbox" checked={third} onChange={(e) => setThird(e.target.checked)} />
            On 3rd
          </label>
          <button type="submit" disabled={busy} className="baseball-debug-card__submit">
            Broadcast custom
          </button>
        </form>
      </div>

      <details className="baseball-debug-card__rawpayload">
        <summary>Last received SignalR payload (raw)</summary>
        <pre>{JSON.stringify(live, null, 2)}</pre>
      </details>
    </div>
  );
}
