import { DateTime } from "luxon";

/**
 * Formats a UTC ISO datetime string into ET.
 * Adds (Day) if not Saturday. Replaces midnight with 'TBD'.
 *
 * @param {string} dateStr - ISO 8601 date string in UTC
 * @param {string} format - Format string for normal times
 * @returns {string} - Formatted string like "Sep 27 (Thu) @ TBD"
 */
export function formatToEasternTime(dateStr, format = "MMM d @ h:mm a") {
  const dtUtc = DateTime.fromISO(dateStr, { zone: "utc" });
  if (!dtUtc.isValid) return "TBD";

  const dtEt = dtUtc.setZone("America/New_York");

  const dayAbbrev = dtEt.toFormat("ccc"); // Mon, Tue, Wed, etc.
  const isSaturday = dtEt.weekday === 6;  // Luxon: 1 = Monday, 7 = Sunday

  const dateLabel = dtEt.toFormat("MMM d") + (isSaturday ? "" : ` (${dayAbbrev})`);

  if (dtEt.hour === 0 && dtEt.minute === 0) {
    return `${dateLabel} @ TBD`;
  }

  const timeLabel = dtEt.toFormat("h:mm a");
  return `${dateLabel} @ ${timeLabel}`;
}
