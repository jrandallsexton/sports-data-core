import LeaguesApi from "../api/leagues/leaguesApi";

/**
 * League-creation availability gate (see
 * docs/features/league-creation-availability-gate.md). Some sports aren't open
 * for league creation until their data is ready — e.g. NCAAFB waits for the AP
 * Poll release. The API exposes the currently-active gates; the create-league
 * UI locks exactly those sports, and the create endpoints enforce the same gate
 * server-side.
 */

/**
 * Fetches the active creation gates as a lookup of backend Sport enum
 * ("FootballNcaa") → the UTC ISO instant it opens. A sport absent from the map
 * is open. Fails OPEN (empty map) on error — the server still enforces the gate,
 * so the worst case is a user seeing a create option the API then rejects.
 * @returns {Promise<Record<string, string>>}
 */
export async function getLeagueCreationGates() {
  try {
    const data = await LeaguesApi.getCreationAvailability();
    const gates = {};
    for (const g of data?.gates ?? []) {
      if (g?.sport && g?.opensUtc) gates[g.sport] = g.opensUtc;
    }
    return gates;
  } catch {
    return {};
  }
}

/**
 * Sports a non-admin can create a league for. MLB is admin-gated in the create
 * flow, so it never factors into non-admin visibility decisions.
 */
export const NON_ADMIN_CREATABLE_SPORTS = ["FootballNcaa", "FootballNfl"];

/**
 * Whether at least one non-admin-creatable sport is open for league creation.
 * The API only returns FUTURE gates (an elapsed gate is omitted), so "absent"
 * means open; the elapsed-instant check is defensive in case that contract
 * ever loosens. Mirrors sd-mobile's anySportOpenForCreation.
 * @param {Record<string, string>} gates
 * @returns {boolean}
 */
export function anySportOpenForCreation(gates) {
  return NON_ADMIN_CREATABLE_SPORTS.some((sport) => {
    const opensUtc = gates?.[sport];
    return !opensUtc || Date.parse(opensUtc) <= Date.now();
  });
}

/**
 * "Aug 17" from a UTC ISO instant. Formatted in UTC so the gate's authored
 * calendar day (a UTC wall-clock) isn't shifted back a day in western
 * timezones. Returns null for an unparseable value.
 */
export function formatGateDate(iso) {
  const d = new Date(iso);
  return Number.isNaN(d.getTime())
    ? null
    : d.toLocaleDateString(undefined, {
        month: "short",
        day: "numeric",
        timeZone: "UTC",
      });
}

/**
 * User-facing variant of {@link formatGateDate}: falls back to "soon" when the
 * instant is missing or unparseable, so a bad value never renders as the literal
 * "null" in a CTA or tab. Use this for display strings; use formatGateDate when
 * you need to distinguish an unparseable value.
 */
export function formatGateDateOrSoon(iso) {
  return formatGateDate(iso) ?? "soon";
}
