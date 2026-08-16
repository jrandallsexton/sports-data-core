import { resolveSportLeague } from './sportLinks';

/**
 * Push-notification deep-link wire contract.
 *
 * Payloads are produced by SportsData.Notification's consumers and arrive on
 * two different paths depending on platform: expo-notifications delivers them
 * as `content.data` on Android, while on iOS RNFirebase owns the FCM message
 * and they land in `remoteMessage.data` (expo's content.data arrives EMPTY —
 * confirmed via Sentry). Both funnel through {@link toPendingDeepLink} so the
 * payload shape lives in exactly one place.
 *
 * Every value is a string on the wire — FCM data payloads are string maps —
 * so anything numeric is parsed here rather than trusted.
 */

/**
 * A tapped notification's navigation target. Discriminated so new push kinds
 * slot in without a state variable per kind.
 */
export type PendingDeepLink =
  | { kind: 'invite'; leagueId: string }
  | {
      kind: 'matchup';
      sport: string;
      league: string;
      contestId: string;
      leagueId?: string;
      week?: number;
    };

/**
 * LeagueInvite contract: kind === 'LeagueInvite' with a string leagueId.
 * Returns the validated leagueId, or null when it's not a LeagueInvite.
 */
export function getLeagueInviteId(
  data: Record<string, unknown> | null | undefined,
): string | null {
  if (!data) return null;
  return data.kind === 'LeagueInvite' && typeof data.leagueId === 'string'
    ? data.leagueId
    : null;
}

/**
 * Notification kinds that land on a game page. Kept as a set rather than a
 * single literal so a new matchup-bound notification only needs an entry here
 * (server side: MatchupDeepLink's *Kind constants).
 *
 * - `OddsChanged` — the line moved on a contest the user picked
 * - `PickScored`  — the user's pick was scored
 */
const MATCHUP_KINDS = new Set(['OddsChanged', 'PickScored']);

/**
 * Matchup deep-link contract: a kind in {@link MATCHUP_KINDS} with a string
 * contestId and the backend Sport enum name. leagueId and week are optional
 * enrichments — leagueId scopes the game page to the user's pick, week narrows
 * the matchup fetch. Sport arrives as the enum ("FootballNcaa") and is mapped
 * to route segments here, so URL conventions stay owned by the client.
 * Returns null when the payload isn't matchup-bound or the sport is unknown.
 */
export function getMatchupTarget(
  data: Record<string, unknown> | null | undefined,
): { sport: string; league: string; contestId: string; leagueId?: string; week?: number } | null {
  if (!data) return null;
  if (typeof data.kind !== 'string' || !MATCHUP_KINDS.has(data.kind)) return null;
  if (typeof data.contestId !== 'string' || typeof data.sport !== 'string') return null;

  const resolved = resolveSportLeague(data.sport);
  if (!resolved) return null;

  // week is a string on the wire; drop it rather than pass NaN downstream.
  // Validate the WHOLE string — parseInt takes a valid prefix, so "3invalid"
  // and "3.5" would both yield 3 and route to a plausible-looking wrong week
  // instead of being rejected. Season weeks are positive integers, so
  // digits-only is the exact contract; isSafeInteger then rejects a digit
  // string long enough to lose precision.
  const rawWeek = typeof data.week === 'string' ? data.week.trim() : '';
  const parsedWeek = /^\d+$/.test(rawWeek) ? Number(rawWeek) : NaN;

  return {
    sport: resolved.sport,
    league: resolved.league,
    contestId: data.contestId,
    leagueId: typeof data.leagueId === 'string' ? data.leagueId : undefined,
    week: Number.isSafeInteger(parsedWeek) ? parsedWeek : undefined,
  };
}

/** Maps a raw FCM data payload to a pending target, or null if unrecognized. */
export function toPendingDeepLink(
  data: Record<string, unknown> | null | undefined,
): PendingDeepLink | null {
  const inviteLeagueId = getLeagueInviteId(data);
  if (inviteLeagueId) return { kind: 'invite', leagueId: inviteLeagueId };

  const matchup = getMatchupTarget(data);
  if (matchup) return { kind: 'matchup', ...matchup };

  return null;
}
