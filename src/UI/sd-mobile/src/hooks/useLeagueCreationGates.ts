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
 * The active creation gates as a lookup of backend Sport enum ("FootballNcaa")
 * → the UTC ISO instant it opens. A sport absent is open. Fails open (empty map)
 * on error — the server still enforces the gate, so the worst case is a user
 * seeing a create option the API then rejects.
 */
export function useLeagueCreationGates(): Record<string, string> {
  const { data } = useQuery({
    queryKey: ['leagues', 'creation-availability'],
    queryFn: () => leaguesApi.getCreationAvailability().then((r) => r.data),
    staleTime: 1000 * 60 * 60, // gate dates barely move; refetch hourly at most
    retry: false, // a failed fetch fails open; don't hammer it
  });

  return useMemo(() => {
    const gates: Record<string, string> = {};
    for (const g of data?.gates ?? []) {
      if (g?.sport && g?.opensUtc) gates[g.sport] = g.opensUtc;
    }
    return gates;
  }, [data]);
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
