SELECT
  c."StartDateUtc",
  c."Id" AS "ContestId",
  fAway."Abbreviation" AS "AwayShort",
  fsAway."Id" AS "AwayFranchiseSeasonId",
  fAway."Slug" AS "AwaySlug",
  fsrdAway."Current" AS "AwayRank",
  fHome."Abbreviation" AS "HomeShort",
  fsHome."Id" AS "HomeFranchiseSeasonId",
  fHome."Slug" AS "HomeSlug",
  fsrdHome."Current" AS "HomeRank",
  co."Details" AS "Spread",
  (co."Spread" * -1) AS "AwaySpread",
  co."Spread" AS "HomeSpread",
  co."OverUnder" AS "OverUnder",
  c."FinalizedUtc",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseSeasonId",
  c."OverUnder" AS "OverUnderResult",
  c."EndDateUtc" AS "CompletedUtc"
FROM public."Contest" c
LEFT JOIN public."Venue" v ON v."Id" = c."VenueId"
INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
LEFT JOIN LATERAL (
  SELECT * FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
  ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."GroupSeason" gsAway ON gsAway."Id" = fsAway."GroupSeasonId"
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
                             FROM public."SeasonWeek" wk WHERE wk."Id" = c."SeasonWeekId")
      ORDER BY spw."DateUtc" DESC, CASE WHEN spw."Type" = 'cfp' THEN 0 ELSE 1 END
      LIMIT 1)
    AND spwe."FranchiseSeasonId" = fsAway."Id"
    AND NOT spwe."IsOtherReceivingVotes" AND NOT spwe."IsDroppedOut"
  LIMIT 1
) fsrdAway ON TRUE
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
INNER JOIN public."GroupSeason" gsHome ON gsHome."Id" = fsHome."GroupSeasonId"
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
                             FROM public."SeasonWeek" wk WHERE wk."Id" = c."SeasonWeekId")
      ORDER BY spw."DateUtc" DESC, CASE WHEN spw."Type" = 'cfp' THEN 0 ELSE 1 END
      LIMIT 1)
    AND spwe."FranchiseSeasonId" = fsHome."Id"
    AND NOT spwe."IsOtherReceivingVotes" AND NOT spwe."IsDroppedOut"
  LIMIT 1
) fsrdHome ON TRUE
WHERE c."Id" = ANY(@ContestIds)
ORDER BY c."StartDateUtc", fHome."Slug"
