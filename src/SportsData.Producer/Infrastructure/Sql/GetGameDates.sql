-- Distinct calendar dates that have at least one scheduled game. Backs the
-- create-league date picker (blackout dates) and the create-time zero-game
-- guard. Sport is implicit — Producer is per-sport, resolved via the client
-- factory.
--
-- Dates bucket to a US calendar date via a FIXED America/New_York: these are US
-- sports, so user/device timezones do not apply. The bucketing only affects the
-- date LABELS returned to the UI; the guard's "is this window empty" answer is
-- governed entirely by the WHERE predicate below.
--
-- The predicate mirrors MatchupScheduleProcessor's window filter exactly
-- (>= from, <= to, null-tolerant), so a non-empty result guarantees matchup
-- generation will find at least one game in the same window.
-- See docs/architecture/league-creation-blackout-dates.md.
SELECT DISTINCT (c."StartDateUtc" AT TIME ZONE 'America/New_York')::date AS "GameDate"
FROM public."Contest" c
WHERE (@FromUtc::timestamptz IS NULL OR c."StartDateUtc" >= @FromUtc::timestamptz)
  AND (@ToUtc::timestamptz   IS NULL OR c."StartDateUtc" <= @ToUtc::timestamptz)
ORDER BY "GameDate";
