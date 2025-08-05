import { DateTime } from "luxon";

export function formatToEasternTime(dateStr, format = "MMM d @ h:mm a") {
  return DateTime
    .fromISO(dateStr, { zone: "utc" })             // Parse as UTC
    .setZone("America/New_York")                   // Convert to ET
    .toFormat(format);
}
