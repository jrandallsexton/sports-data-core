import type { League, PickAccuracyByWeek, WeeklyAccuracy } from '@/src/types/models';

/**
 * The leagues worth charting: ACTIVE (present in /user/me) and with at least
 * one graded week. Ordered as /user/me orders them, so the picker matches
 * YourLeaguesCard. GET /ui/picks/chart carries every league the user has
 * ever belonged to and no season filter, which is why the intersection is
 * done here rather than trusting the chart's own list.
 */
export function activeAccuracyLeagues(
  chart: PickAccuracyByWeek[] | undefined,
  activeLeagues: League[],
): PickAccuracyByWeek[] {
  if (!chart?.length || !activeLeagues.length) return [];
  const byId = new Map(chart.map((c) => [c.leagueId, c]));
  const out: PickAccuracyByWeek[] = [];
  for (const league of activeLeagues) {
    const entry = byId.get(league.id);
    if (!entry) continue;
    const graded = entry.weeklyAccuracy.filter((w) => w.totalPicks > 0);
    if (graded.length === 0) continue;
    out.push({ ...entry, weeklyAccuracy: graded });
  }
  return out;
}

/** Mean of the weekly percentages (what the web widget draws as its reference line). */
export function meanAccuracy(weeks: WeeklyAccuracy[]): number {
  if (weeks.length === 0) return 0;
  return weeks.reduce((sum, w) => sum + w.accuracyPercent, 0) / weeks.length;
}

/** Season totals across the graded weeks: correct / total picks. */
export function seasonTotals(weeks: WeeklyAccuracy[]): { correct: number; total: number } {
  return weeks.reduce(
    (acc, w) => ({ correct: acc.correct + w.correctPicks, total: acc.total + w.totalPicks }),
    { correct: 0, total: 0 },
  );
}

/** "64%" for whole numbers, "64.2%" otherwise; the API already rounds to one decimal. */
export function formatPercent(value: number): string {
  return Number.isInteger(value) ? `${value}%` : `${value.toFixed(1)}%`;
}
