SELECT "Id"
FROM public."Contest"
WHERE "FinalizedUtc" IS NOT NULL
  AND "SeasonWeekId" = @SeasonWeekId;
