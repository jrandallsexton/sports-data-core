
SELECT
  sw."Id" as "SeasonWeekId",
  s."Year" AS "SeasonYear",
  sw."Number" AS "SeasonWeek",
  c."Id" AS "ContestId",
  c."StartDateUtc" AS "StartDateUtc",
  cs."StatusTypeName" as "Status",
  cs."StatusDescription" as "StatusDescription",

  v."Name"                  as "VenueName",
  v."City"                  as "VenueCity",
  v."State"                 as "VenueState",
  v."Latitude"              as "VenueLatitude",
  v."Longitude"             as "VenueLongitude",

  fAway."Slug"              as "AwaySlug",
  fAway."ColorCodeHex"      as "AwayColor",
  fAway."Abbreviation"      as "AwayAbbreviation",
  fsrdAway."Current"        as "AwayRank",
  fsAway."Wins"             as "AwayWins",
  fsAway."Losses"           as "AwayLosses",
  fsAway."ConferenceWins"   as "AwayConferenceWins",
  fsAway."ConferenceLosses" as "AwayConferenceLosses",
  gsAway."Slug"             as "AwayConferenceSlug",  

  fHome."Slug"              as "HomeSlug",
  fHome."ColorCodeHex"      as "HomeColor",
  fHome."Abbreviation"      as "HomeAbbreviation",
  fsrdHome."Current"        as "HomeRank",
  fsHome."Wins"             as "HomeWins",
  fsHome."Losses"           as "HomeLosses",
  fsHome."ConferenceWins"   as "HomeConferenceWins",
  fsHome."ConferenceLosses" as "HomeConferenceLosses",
  gsHome."Slug"             as "HomeConferenceSlug",

  co."Details"        as "Spread",
  (co."Spread" * -1)  as "AwaySpread",
  co."Spread"         as "HomeSpread",
  co."OverUnder"      as "OverUnder",
  co."OverOdds"       as "OverOdds",
  co."UnderOdds"      as "UnderOdds"
FROM public."SeasonWeek" sw
INNER JOIN public."Season" s on s."Id" = sw."SeasonId"
inner join public."Contest" c ON c."SeasonWeekId" = sw."Id"
inner join public."Competition" comp on comp."ContestId" = c."Id"

-- Use LATERAL join to prioritize ESPN (58) over DraftKings (100)
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" 
    AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE

left  join public."CompetitionStatus" cs on cs."CompetitionId" = comp."Id"
inner join public."Venue" v on v."Id" = c."VenueId"
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
inner join public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
inner join public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
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
      WHERE sp."SeasonYear" = fsAway."SeasonYear"
        AND spw."Type" IN ('ap', 'cfp')
        AND spw."DateUtc" < (SELECT wk."StartDate" + INTERVAL '5 days'
                             FROM public."SeasonWeek" wk WHERE wk."Id" = sw."Id")
      ORDER BY spw."DateUtc" DESC, CASE WHEN spw."Type" = 'cfp' THEN 0 ELSE 1 END
      LIMIT 1)
    AND spwe."FranchiseSeasonId" = fsAway."Id"
    AND NOT spwe."IsOtherReceivingVotes" AND NOT spwe."IsDroppedOut"
  LIMIT 1
) fsrdAway ON TRUE
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
      WHERE sp."SeasonYear" = fsHome."SeasonYear"
        AND spw."Type" IN ('ap', 'cfp')
        AND spw."DateUtc" < (SELECT wk."StartDate" + INTERVAL '5 days'
                             FROM public."SeasonWeek" wk WHERE wk."Id" = sw."Id")
      ORDER BY spw."DateUtc" DESC, CASE WHEN spw."Type" = 'cfp' THEN 0 ELSE 1 END
      LIMIT 1)
    AND spwe."FranchiseSeasonId" = fsHome."Id"
    AND NOT spwe."IsOtherReceivingVotes" AND NOT spwe."IsDroppedOut"
  LIMIT 1
) fsrdHome ON TRUE
WHERE s."Year" = @SeasonYear and sw."Number" = @SeasonWeekNumber
ORDER BY "StartDateUtc", fHome."Slug"