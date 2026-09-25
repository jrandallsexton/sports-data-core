// Pure helpers for the StatBot advisor sheet — level metadata, reason lines
// composed from the advice endpoint's structured facts (numbers from the
// server, never prose), the apply set, and the standings sentences. Mirrors
// sd-ui/src/components/picks/advisorSheet.js one-for-one so both platforms
// say the same thing. See docs/features/statbot-advisor.md.
//
// Names, per the product: the probability is the **deetsMeter** (the
// statistical model); **StatBot** is the written preview and its pick.

import type {
  AdvisedPick,
  AdvisorLevel,
  PickAdviceAnalysis,
  UserPick,
} from '@/src/types/models';

export interface AdvisorLevelMeta {
  key: AdvisorLevel;
  name: string;
  /** Short enough for a segmented control on a phone. */
  short: string;
  tagline: string;
  detail: string;
}

/**
 * Working names — the count of levels is the contract, the football names
 * get their naming pass once the count settles. Order = increasing risk.
 */
export const ADVISOR_LEVELS: AdvisorLevelMeta[] = [
  {
    key: 'Prevent',
    name: 'Prevent',
    short: 'Prevent',
    tagline: 'Protect what you have',
    detail: "The deetsMeter's side in every game, most points on the surest ones.",
  },
  {
    key: 'GoalLine',
    name: 'Goal-line',
    short: 'Goal-line',
    tagline: 'One upset, low stakes',
    detail: 'Flip the closest coin flip; it gets a mid-sheet value.',
  },
  {
    key: 'QbDraw',
    name: 'QB Draw',
    short: 'QB Draw',
    tagline: 'A few upsets, stakes protected',
    detail: 'Flip the two or three closest coin flips; the locks keep the top points.',
  },
  {
    key: 'HailMary',
    name: 'Hail Mary',
    short: 'Hail Mary',
    tagline: 'Swing for the week',
    detail: 'Flip every coin flip and put the top points on them.',
  },
];

export function levelMeta(key: AdvisorLevel | null | undefined): AdvisorLevelMeta {
  return ADVISOR_LEVELS.find((l) => l.key === key) ?? ADVISOR_LEVELS[0];
}

/** 0.7312 → "73%". Null-safe. */
export function formatProbability(p: number | null | undefined): string | null {
  if (p == null || Number.isNaN(p)) return null;
  return `${Math.round(p * 100)}%`;
}

export function kindLabel(kind: AdvisedPick['kind']): string {
  switch (kind) {
    case 'Lock':
      return 'Lock';
    case 'Lean':
      return 'Lean';
    case 'Flip':
      return 'Flip';
    case 'Locked':
      return 'Locked';
    case 'NoPrediction':
      return 'No call';
    default:
      return kind;
  }
}

/**
 * One line per advised pick, composed from facts the server sent. Wording
 * speaks in win probability only — no spreads — so it needs no gambling gate.
 * `previewAgrees` is "StatBot agrees with the deetsMeter".
 */
export function describePick(pick: Pick<AdvisedPick, 'kind' | 'modelProbability' | 'previewAgrees'>): string {
  const pct = formatProbability(pick.modelProbability);
  const statBot =
    pick.previewAgrees === true
      ? 'StatBot agrees'
      : pick.previewAgrees === false
        ? 'StatBot disagrees'
        : null;

  switch (pick.kind) {
    case 'Locked':
      return 'Already kicked off — untouched';
    case 'NoPrediction':
      return 'No deetsMeter number — pick this one yourself';
    case 'Lock':
      return [`deetsMeter ${pct} — lock`, statBot].filter(Boolean).join(', ');
    case 'Lean':
      // A sure-looking number can still be a coin flip when StatBot took
      // the other side; say that, or "78% … coin flip" reads as a bug.
      return pick.previewAgrees === false
        ? `deetsMeter ${pct} but StatBot disagrees — treated as a coin flip, staying with the deetsMeter`
        : [`Coin flip (${pct}) — staying with the deetsMeter`, statBot].filter(Boolean).join(', ');
    case 'Flip':
      // The advised side is the other side, so pct reads below 50 — or
      // StatBot's side when the two disagreed.
      return pick.previewAgrees === false
        ? `Coin flip — deetsMeter and StatBot split; taking StatBot's side (${pct})`
        : `Coin flip — taking the other side (${pct}) to gain ground`;
    default:
      return '';
  }
}

/**
 * The picks the Apply button will write: a side to submit, not locked, and
 * different from what the user already has. Locked and blank rows never
 * apply; unchanged rows are skipped so a re-apply is a no-op. In a
 * confidence league a row without a value is never submitted either — the
 * server rejects it, which would abort the loop mid-sheet.
 */
export function applicablePicks(
  picks: AdvisedPick[] | null | undefined,
  useConfidencePoints = false,
): AdvisedPick[] {
  return (picks ?? []).filter(
    (p) =>
      !!p.franchiseSeasonId &&
      p.kind !== 'Locked' &&
      p.kind !== 'NoPrediction' &&
      p.differsFromExisting &&
      (!useConfidencePoints || p.confidencePoints != null),
  );
}

/**
 * Apply-button copy that says what will actually happen. `differsFromExisting`
 * alone can't tell a blank slate from a rewrite, so the page's current picks
 * decide: rows with no pick are "set", rows with one are "replaced".
 */
export function applyLabel(
  toApply: AdvisedPick[],
  userPicks: ReadonlyMap<string, Pick<UserPick, 'franchiseSeasonId'>> | null | undefined,
): string {
  const total = toApply.length;
  if (total === 0) return 'Nothing to change';
  const replacing = toApply.filter((p) => !!userPicks?.get(p.contestId)?.franchiseSeasonId).length;
  const setting = total - replacing;
  const n = (count: number, verb: string) => `${verb} ${count} pick${count === 1 ? '' : 's'}`;
  if (replacing === 0) return n(setting, 'Set');
  if (setting === 0) return n(replacing, 'Replace');
  return `Set ${setting}, replace ${replacing}`;
}

const ordinal = (r: number): string => {
  const s = ['th', 'st', 'nd', 'rd'];
  const v = r % 100;
  return `${r}${s[(v - 20) % 10] ?? s[v] ?? s[0]}`;
};

const n = (count: number, noun: string) => `${count} ${noun}${count === 1 ? '' : 's'}`;

/**
 * Standings sentence for the analysis card. Performance and standings only —
 * never picks. Only this week's slate is a fact (future slates are never
 * forecast) and no per-game target is ever shown. Reachability assumes the
 * people ahead score at their per-game pace on this slate.
 */
export function describeStanding(analysis: PickAdviceAnalysis | null | undefined): string {
  if (!analysis) return '';
  if (analysis.rank == null) return 'No scored weeks yet — nothing to chase.';

  const thisWeek = `${n(analysis.gamesThisWeek, 'game')} this week, up to ${n(
    analysis.maxPointsThisWeek,
    'point',
  )} on the table.`;
  const weeks =
    analysis.regularSeasonWeeksLeft != null
      ? ` ${n(analysis.regularSeasonWeeksLeft, 'week')} left in the regular season.`
      : '';

  if (analysis.deficit <= 0) {
    return `You're leading. ${thisWeek}${weeks}`;
  }

  const leader = analysis.leaderName ?? 'the leader';
  const where = `You're ${ordinal(analysis.rank)}, ${n(analysis.deficit, 'point')} behind ${leader}.`;

  const nextAhead =
    analysis.nextAheadName && analysis.pointsBehindNextAhead != null && analysis.nextAheadRank != null
      ? `${analysis.nextAheadName} in ${ordinal(analysis.nextAheadRank)} is ${n(
          analysis.pointsBehindNextAhead,
          'point',
        )} ahead${
          analysis.nextAheadExpectedThisWeek != null
            ? ` and, at their per-game pace, adds about ${analysis.nextAheadExpectedThisWeek} on this slate`
            : ''
        }`
      : null;

  let reach: string;
  if (analysis.canCloseGapThisWeek) {
    reach = `A perfect week takes the lead even if ${leader} adds about ${analysis.leaderExpectedThisWeek} on this slate at their per-game pace.`;
  } else if (analysis.bestCaseRankThisWeek != null && analysis.bestCaseRankThisWeek < analysis.rank) {
    reach = `With everyone at pace, a perfect week moves you to ${ordinal(
      analysis.bestCaseRankThisWeek,
    )} at best.${nextAhead ? ` ${nextAhead}.` : ''}`;
  } else {
    reach = nextAhead
      ? `${nextAhead} — out of reach this week, so play for next.`
      : 'Nobody is within reach this week, so play for next.';
  }

  return `${where} ${thisWeek}${weeks} ${reach}`;
}

/** StatBot's own standing, phrased honestly. Null when StatBot isn't a scored member. */
export function describeStatBot(analysis: PickAdviceAnalysis | null | undefined): string | null {
  const bot = analysis?.statBot;
  if (!bot) return null;
  const behindYou = analysis?.rank != null && bot.rank > analysis.rank;
  return behindYou
    ? `StatBot is #${bot.rank} in this league — behind you. Take that as you will.`
    : `StatBot is #${bot.rank} in this league, ${bot.pointsPerGame} points per game.`;
}
