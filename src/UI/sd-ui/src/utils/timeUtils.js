
import { DateTime } from "luxon";

export const DEFAULT_TIMEZONE = "America/New_York";

/**
 * The "expected" game day for a sport/league, as a Luxon weekday number
 * (Mon=1 … Sun=7), or null when the sport has no dominant day. Nullable by
 * design: null means "always show the day".
 *
 * Accepts either the backend Sport enum name ("FootballNcaa",
 * "FootballNfl") or slug fragments (e.g. "football-ncaa" built from route
 * params) — match is case-insensitive substring, same convention as
 * getStartLabel.
 */
export function getDefaultGameWeekday(sportOrLeague) {
  const s = (sportOrLeague ?? "").toLowerCase();
  if (!s.includes("football")) return null;
  if (s.includes("ncaa")) return 6; // NCAAFB: Saturday
  if (s.includes("nfl")) return 7;  // NFL: Sunday
  return null;
}

/**
 * Format a UTC ISO datetime string as a calendar+time label in the given IANA zone.
 * Returns "MMM d (Day) @ h:mm a" (e.g. "Sep 27 (Thu) @ 1:30 PM"); the (Day) is
 * omitted when the game falls on `defaultWeekday` — the sport's expected game
 * day (see getDefaultGameWeekday). The parameter defaults to Saturday, the
 * app's original NCAAFB-only behavior, so legacy callers are unchanged; pass
 * null to always show the day. Midnight in the target zone renders as "TBD"
 * because the backend uses 00:00 to mean "time not yet known."
 *
 * @param {string} dateStr - ISO 8601 date string in UTC
 * @param {string} [timezone] - IANA zone (e.g. "America/Chicago"); defaults to ET
 * @param {number|null} [defaultWeekday] - Luxon weekday (Mon=1…Sun=7) to omit, or null
 * @returns {string}
 */
export function formatToUserTime(dateStr, timezone = DEFAULT_TIMEZONE, defaultWeekday = 6) {
  const dtUtc = DateTime.fromISO(dateStr, { zone: "utc" });
  if (!dtUtc.isValid) return "TBD";

  const zone = timezone || DEFAULT_TIMEZONE;
  const dtLocal = dtUtc.setZone(zone);
  if (!dtLocal.isValid) {
    return formatToUserTime(dateStr, DEFAULT_TIMEZONE, defaultWeekday);
  }

  const dayAbbrev = dtLocal.toFormat("ccc");
  const isDefaultDay = defaultWeekday != null && dtLocal.weekday === defaultWeekday;
  const dateLabel = dtLocal.toFormat("MMM d") + (isDefaultDay ? "" : ` (${dayAbbrev})`);

  if (dtLocal.hour === 0 && dtLocal.minute === 0) {
    return `${dateLabel} @ TBD`;
  }

  const timeLabel = dtLocal.toFormat("h:mm a");
  return `${dateLabel} @ ${timeLabel}`;
}

/**
 * Sport-aware label for a game's scheduled start time. Accepts either the
 * URL sport slug ("football", "baseball") or the backend Sport enum name
 * ("FootballNcaa", "BaseballMlb") — match is case-insensitive substring.
 */
export function getStartLabel(sport) {
  const s = (sport ?? "").toLowerCase();
  if (s.includes("baseball")) return "First Pitch";
  if (s.includes("football")) return "Kickoff";
  return "Start Time";
}

/**
 * Returns the abbreviation (e.g. "EDT", "CST", "GMT+9") for a given IANA zone.
 * Pass `gameDateIso` for a per-game label so the abbreviation reflects the
 * game's actual DST status (e.g. an October NCAAFB game viewed in May should
 * read "EDT" if it falls before the Nov DST switch, "EST" otherwise) rather
 * than today's. Falls back to "now" when no date is provided — appropriate
 * for column headers spanning multiple games.
 */
export function getZoneAbbreviation(timezone = DEFAULT_TIMEZONE, gameDateIso) {
  const zone = timezone || DEFAULT_TIMEZONE;
  const base = gameDateIso
    ? DateTime.fromISO(gameDateIso, { zone: "utc" }).setZone(zone)
    : DateTime.now().setZone(zone);
  if (!base.isValid) return "ET";
  return base.toFormat("ZZZZ");
}

/**
 * Backwards-compatible alias that always renders in Eastern Time. Prefer
 * `formatToUserTime(dateStr, useUserTimeZone())` in new code so the user's
 * configured timezone is honored.
 */
export function formatToEasternTime(dateStr) {
  return formatToUserTime(dateStr, DEFAULT_TIMEZONE);
}

/**
 * Formats a UTC ISO datetime string as M/D in the given IANA zone.
 */
export function formatToMonthDay(dateStr, timezone = DEFAULT_TIMEZONE) {
  const dtUtc = DateTime.fromISO(dateStr, { zone: "utc" });
  if (!dtUtc.isValid) return "-";
  const zone = timezone || DEFAULT_TIMEZONE;
  const dtLocal = dtUtc.setZone(zone);
  if (!dtLocal.isValid) {
    return formatToMonthDay(dateStr, DEFAULT_TIMEZONE);
  }
  return dtLocal.toFormat("M/d");
}
