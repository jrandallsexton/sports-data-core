WITH next_week AS (
  SELECT sw."Id" AS "SeasonWeekId",
         sw."Number" AS "WeekNumber",
         s."Id" AS "SeasonId",
         s."Year" AS "SeasonYear"
  FROM public."Season" s
  JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
  JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
  WHERE sp."Name" = 'Regular Season'
    AND sw."StartDate" <= CURRENT_DATE and sw."EndDate" > CURRENT_DATE
  ORDER BY sw."StartDate"
  LIMIT 1
)

SELECT DISTINCT ON (F."Id")
	FS."Id" AS "FranchiseSeasonId",
	F."Slug" AS "Slug",
	F."DisplayName" AS "Name",
	F."DisplayNameShort" AS "ShortName",
	fsrd."Current" AS "Ranking",
	GS."Name" AS "ConferenceName",
	GS."ShortName" AS "ConferenceShortName",
	GS."Slug" AS "ConferenceSlug",
	FS."Wins" || '-' || FS."Losses" || '-' || FS."Ties" AS "OverallRecord",
	FS."ConferenceWins" || '-' || FS."ConferenceLosses" || '-' || FS."ConferenceTies" AS "ConferenceRecord",
	F."ColorCodeHex" AS "ColorPrimary",
	F."ColorCodeAltHex" AS "ColorSecondary",
	FL."Uri" AS "LogoUrl",
	NULL AS "HelmetUrl",
	F."Location" AS "Location",
	V."Name" AS "StadiumName",
	V."Capacity" AS "StadiumCapacity"
FROM
	PUBLIC."Franchise" F
	INNER JOIN PUBLIC."FranchiseSeason" FS on FS."FranchiseId" = F."Id"
	LEFT JOIN LATERAL (
  -- Rank from the SeasonPoll store (the store the weekly rankings job
  -- feeds). POLL-FIRST: find THE poll in effect (the week's DESIGNATED poll: latest published
  -- before the week's start + 5 days, admitting the entering Sunday AP
  -- poll and the midweek Tuesday CFP poll but not the NEXT Sunday's AP), then this
  -- team's entry in it — a team that dropped out is honestly unranked,
  -- instead of retaining its last ranked appearance forever (both this
  -- query's old form and the old store had that sticky-rank flaw).
  -- 'cfp' preferred over 'ap' (stand-in for the old store's
  -- DefaultRanking flag). Keyed on DateUtc, NOT
  -- SeasonPollWeek.SeasonWeekId — those links are unreliable
  -- (off-by-one late season, NULL for preseason/final).
  SELECT spwe."Current"
  FROM public."SeasonPollWeekEntry" spwe
  WHERE spwe."SeasonPollWeekId" = (
      SELECT spw."Id"
      FROM public."SeasonPollWeek" spw
      INNER JOIN public."SeasonPoll" sp ON sp."Id" = spw."SeasonPollId"
      WHERE sp."SeasonYear" = FS."SeasonYear"
        AND spw."Type" IN ('ap', 'cfp')
        AND spw."DateUtc" < (SELECT wk."StartDate" + INTERVAL '5 days'
                             FROM public."SeasonWeek" wk WHERE wk."Id" = (select "SeasonWeekId" from next_week))
      ORDER BY spw."DateUtc" DESC, CASE WHEN spw."Type" = 'cfp' THEN 0 ELSE 1 END
      LIMIT 1)
    AND spwe."FranchiseSeasonId" = FS."Id"
    AND NOT spwe."IsOtherReceivingVotes" AND NOT spwe."IsDroppedOut"
  LIMIT 1
) fsrd ON TRUE
	INNER JOIN PUBLIC."GroupSeason" GS ON GS."Id" = FS."GroupSeasonId"
	LEFT JOIN PUBLIC."FranchiseLogo" FL ON FL."FranchiseId" = F."Id"
	LEFT JOIN PUBLIC."Venue" V ON V."Id" = F."VenueId"
WHERE
	F."Slug" = @Slug and FS."SeasonYear" = @SeasonYear
ORDER BY
    F."Id",
    FL."CreatedUtc" ASC NULLS LAST;