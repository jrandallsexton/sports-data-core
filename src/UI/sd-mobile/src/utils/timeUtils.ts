/**
 * Time/date formatting helpers for the mobile app. Mirrors the web util at
 * `src/UI/sd-ui/src/utils/timeUtils.js` so both clients render game times
 * identically. Uses the platform's native `Intl.DateTimeFormat` with the
 * `timeZone` option — Hermes (Expo 55 / RN 0.83) ships full ICU support so
 * we don't need Luxon or date-fns-tz.
 *
 * Backend convention: midnight (00:00) in the rendered zone means "time not
 * yet known," and we surface that to the user as "TBD". Saturday formatting
 * deliberately omits the day-of-week pip — Saturday is the default game day
 * for college football and the bare date reads cleaner.
 */

export const DEFAULT_TIMEZONE = 'America/New_York';

const MONTHS_SHORT = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

// Intl.DateTimeFormat returns weekday strings like "Mon", "Tue", etc. when
// formatted with weekday: 'short' in en-US, which is exactly what we want.
// The "(Day)" suffix is stripped for Saturday.

interface ZonedParts {
  year: number;
  month: number; // 1-12
  day: number;
  hour: number; // 0-23
  minute: number;
  weekday: string; // "Mon", "Tue", …
  hour12: number; // 1-12 for "h:mm a" rendering
  dayPeriod: 'AM' | 'PM';
}

function zonedParts(date: Date, timeZone: string): ZonedParts | null {
  try {
    const formatter = new Intl.DateTimeFormat('en-US', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      weekday: 'short',
    });
    const parts = formatter.formatToParts(date);
    const get = (type: Intl.DateTimeFormatPartTypes) =>
      parts.find((p) => p.type === type)?.value ?? '';

    const year = Number(get('year'));
    const month = Number(get('month'));
    const day = Number(get('day'));
    let hour = Number(get('hour'));
    const minute = Number(get('minute'));
    const weekday = get('weekday');

    if (
      Number.isNaN(year) ||
      Number.isNaN(month) ||
      Number.isNaN(day) ||
      Number.isNaN(hour) ||
      Number.isNaN(minute)
    ) {
      return null;
    }

    // Some hosts return "24" for midnight under hour12: false. Normalize.
    if (hour === 24) hour = 0;

    const hour12 = hour % 12 === 0 ? 12 : hour % 12;
    const dayPeriod = hour < 12 ? 'AM' : 'PM';

    return { year, month, day, hour, minute, weekday, hour12, dayPeriod };
  } catch {
    return null;
  }
}

/**
 * Format a UTC ISO datetime string as a calendar+time label in the given
 * IANA zone. Returns "MMM d (Day) @ h:mm a" (e.g. "Sep 27 (Thu) @ 1:30 PM");
 * the (Day) is omitted on Saturdays. Midnight in the target zone renders as
 * "TBD" because the backend uses 00:00 to mean "time not yet known."
 *
 * @param dateStr  ISO 8601 datetime string (assumed UTC)
 * @param timezone IANA zone (e.g. "America/Chicago"); defaults to ET
 */
export function formatToUserTime(
  dateStr: string | null | undefined,
  timezone: string | null | undefined = DEFAULT_TIMEZONE,
): string {
  if (!dateStr) return 'TBD';
  const d = new Date(dateStr);
  if (Number.isNaN(d.getTime())) return 'TBD';

  const zone = timezone || DEFAULT_TIMEZONE;
  let parts = zonedParts(d, zone);
  if (!parts && zone !== DEFAULT_TIMEZONE) {
    parts = zonedParts(d, DEFAULT_TIMEZONE);
  }
  if (!parts) return 'TBD';

  const monthLabel = MONTHS_SHORT[parts.month - 1] ?? String(parts.month);
  const isSaturday = parts.weekday === 'Sat';
  const dateLabel =
    `${monthLabel} ${parts.day}` + (isSaturday ? '' : ` (${parts.weekday})`);

  if (parts.hour === 0 && parts.minute === 0) {
    return `${dateLabel} @ TBD`;
  }

  const minuteStr = String(parts.minute).padStart(2, '0');
  const timeLabel = `${parts.hour12}:${minuteStr} ${parts.dayPeriod}`;
  return `${dateLabel} @ ${timeLabel}`;
}

/**
 * Sport-aware label for a game's scheduled start time. Accepts either the
 * URL sport slug ("football", "baseball") or the backend Sport enum name
 * ("FootballNcaa", "BaseballMlb") — match is case-insensitive substring.
 */
export function getStartLabel(sport: string | null | undefined): string {
  const s = (sport ?? '').toLowerCase();
  if (s.includes('baseball')) return 'First Pitch';
  if (s.includes('football')) return 'Kickoff';
  return 'Start Time';
}

/**
 * Returns the abbreviation (e.g. "EDT", "CST", "GMT+9") for a given IANA
 * zone at the current moment. Used to label kickoff columns/rows. Falls back
 * to "ET" when the platform Intl can't produce a short tz name.
 */
export function getZoneAbbreviation(
  timezone: string | null | undefined = DEFAULT_TIMEZONE,
): string {
  const zone = timezone || DEFAULT_TIMEZONE;
  try {
    const formatter = new Intl.DateTimeFormat('en-US', {
      timeZone: zone,
      timeZoneName: 'short',
    });
    const parts = formatter.formatToParts(new Date());
    const tz = parts.find((p) => p.type === 'timeZoneName')?.value;
    return tz || 'ET';
  } catch {
    return 'ET';
  }
}

/**
 * Backwards-compatible alias that always renders in Eastern Time. Prefer
 * `formatToUserTime(dateStr, useUserTimeZone())` in new code so the user's
 * configured timezone is honored.
 */
export function formatToEasternTime(dateStr: string | null | undefined): string {
  return formatToUserTime(dateStr, DEFAULT_TIMEZONE);
}

/**
 * Formats a UTC ISO datetime string as M/D in the given IANA zone.
 */
export function formatToMonthDay(
  dateStr: string | null | undefined,
  timezone: string | null | undefined = DEFAULT_TIMEZONE,
): string {
  if (!dateStr) return '-';
  const d = new Date(dateStr);
  if (Number.isNaN(d.getTime())) return '-';
  const zone = timezone || DEFAULT_TIMEZONE;
  let parts = zonedParts(d, zone);
  if (!parts && zone !== DEFAULT_TIMEZONE) {
    parts = zonedParts(d, DEFAULT_TIMEZONE);
  }
  if (!parts) return '-';
  return `${parts.month}/${parts.day}`;
}
