
import { DateTime } from "luxon";

export const DEFAULT_TIMEZONE = "America/New_York";

/**
 * Format a UTC ISO datetime string as a calendar+time label in the given IANA zone.
 * Returns "MMM d (Day) @ h:mm a" (e.g. "Sep 27 (Thu) @ 1:30 PM"); the (Day) is
 * omitted on Saturdays. Midnight in the target zone renders as "TBD" because the
 * backend uses 00:00 to mean "time not yet known."
 *
 * @param {string} dateStr - ISO 8601 date string in UTC
 * @param {string} [timezone] - IANA zone (e.g. "America/Chicago"); defaults to ET
 * @returns {string}
 */
export function formatToUserTime(dateStr, timezone = DEFAULT_TIMEZONE) {
  const dtUtc = DateTime.fromISO(dateStr, { zone: "utc" });
  if (!dtUtc.isValid) return "TBD";

  const zone = timezone || DEFAULT_TIMEZONE;
  const dtLocal = dtUtc.setZone(zone);
  if (!dtLocal.isValid) {
    return formatToUserTime(dateStr, DEFAULT_TIMEZONE);
  }

  const dayAbbrev = dtLocal.toFormat("ccc");
  const isSaturday = dtLocal.weekday === 6;
  const dateLabel = dtLocal.toFormat("MMM d") + (isSaturday ? "" : ` (${dayAbbrev})`);

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
