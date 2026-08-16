import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

import { leaguesApi } from '@/src/services/api/leaguesApi';

/**
 * League-creation availability gate (see
 * docs/features/league-creation-availability-gate.md). Some sports aren't open
 * for league creation until their data is ready — e.g. NCAAFB waits for the AP
 * Poll release. The create-league UI hides/notes gated sports; the create
 * endpoints enforce the same gate server-side.
 */

const MONTHS_SHORT = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
];

/**
 * Sports a non-admin can create a league for. MLB is admin-gated in the create
 * flow, so it never factors into non-admin visibility decisions.
 */
export const NON_ADMIN_CREATABLE_SPORTS = ['FootballNcaa', 'FootballNfl'] as const;

/**
 * Pending-aware variant of {@link useLeagueCreationGates} for surfaces that
 * HIDE an affordance while every sport is gated (e.g. the leagues screen's
 * "+ Create"): without the pending flag those surfaces would flash the
 * affordance during the initial fetch (empty map = "everything open") and then
 * yank it. A resolved failure still fails open — the server enforces the gate.
 */
export function useLeagueCreationGatesState(): {
  gates: Record<string, string>;
  isPending: boolean;
} {
  const { data, isPending } = useQuery({
    queryKey: ['leagues', 'creation-availability'],
    queryFn: () => leaguesApi.getCreationAvailability().then((r) => r.data),
    staleTime: 1000 * 60 * 60, // gate dates barely move; refetch hourly at most
    retry: false, // a failed fetch fails open; don't hammer it
  });

  const gates = useMemo(() => {
    const map: Record<string, string> = {};
    for (const g of data?.gates ?? []) {
      if (g?.sport && g?.opensUtc) map[g.sport] = g.opensUtc;
    }
    return map;
  }, [data]);

  return { gates, isPending };
}

/**
 * The active creation gates as a lookup of backend Sport enum ("FootballNcaa")
 * → the UTC ISO instant it opens. A sport absent is open. Fails open (empty map)
 * on error — the server still enforces the gate, so the worst case is a user
 * seeing a create option the API then rejects.
 */
export function useLeagueCreationGates(): Record<string, string> {
  return useLeagueCreationGatesState().gates;
}

/**
 * Whether at least one non-admin-creatable sport is open for league creation.
 * The API only returns FUTURE gates (an elapsed gate is omitted), so "absent"
 * means open; the elapsed-instant check is defensive in case that contract
 * ever loosens.
 */
export function anySportOpenForCreation(gates: Record<string, string>): boolean {
  return NON_ADMIN_CREATABLE_SPORTS.some((sport) => {
    const opensUtc = gates[sport];
    return !opensUtc || Date.parse(opensUtc) <= Date.now();
  });
}

/**
 * "Aug 17" from a UTC ISO instant. Uses UTC getters so the gate's authored
 * calendar day isn't shifted a day in western timezones, and avoids Intl (not
 * guaranteed on Hermes). Returns null for an unparseable value.
 */
export function formatGateDate(iso: string): string | null {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return `${MONTHS_SHORT[d.getUTCMonth()]} ${d.getUTCDate()}`;
}

/**
 * User-facing variant of {@link formatGateDate}: falls back to "soon" when the
 * instant is missing or unparseable, so a bad value never renders as the literal
 * "null" in a CTA or note. Use this for display strings; use formatGateDate when
 * you need to distinguish an unparseable value.
 */
export function formatGateDateOrSoon(iso: string): string {
  return formatGateDate(iso) ?? 'soon';
}
