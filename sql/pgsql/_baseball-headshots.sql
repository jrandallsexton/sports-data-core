SELECT
  ats."Id"                              AS AthleteSeasonId,
  ats."ShortName",
  ats."AthleteId",
  a."Id"                                AS AthleteIdResolved,
  a."DisplayName",
  (SELECT COUNT(*) FROM "AthleteImage" ai WHERE ai."AthleteId" = a."Id") AS ImageCount
FROM "AthleteSeason" ats
LEFT JOIN "Athlete" a ON a."Id" = ats."AthleteId"
WHERE ats."Id" IN ('<atBatAthleteSeasonId>', '<pitchingAthleteSeasonId>');