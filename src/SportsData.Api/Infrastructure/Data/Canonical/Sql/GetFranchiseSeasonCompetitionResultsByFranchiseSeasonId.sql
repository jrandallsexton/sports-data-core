SELECT
  c."StartDateUtc",
  c."Id" AS "ContestId",

  fAway."Abbreviation" as "AwayShort",
  fsAway."Id" as "AwayFranchiseSeasonId",
  fAway."Slug" as "AwaySlug",
  fsrdAway."Current" as "AwayRank",

  fHome."Abbreviation" as "HomeShort",
  fsHome."Id" as "HomeFranchiseSeasonId",
  fHome."Slug" as "HomeSlug",
  fsrdHome."Current" as "HomeRank",
  
  co."Details" as "Spread",
  (co."Spread" * -1) as "AwaySpread",
  co."Spread" as "HomeSpread",
  co."OverUnder" as "OverUnder",

  c."FinalizedUtc",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseSeasonId",
  c."OverUnder" as "OverUnderResult",
  c."EndDateUtc" as "CompletedUtc"

FROM public."Contest" c
INNER JOIN public."Venue" v on v."Id" = c."VenueId"
INNER JOIN public."Competition" comp on comp."ContestId" = c."Id"

-- Use LATERAL join to prioritize ESPN (58) over DraftKings (100)
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" 
    AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE

INNER JOIN public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"

INNER JOIN public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"

LEFT JOIN LATERAL (
  -- Rank via poll_rank_asof — the single poll-rank definition (see the
  -- PollRankAsofFunction migration): the poll in effect at kickoff,
  -- this team's entry in it, or NULL = honestly unranked.
  SELECT public.poll_rank_asof(fsAway."Id", fsAway."SeasonYear", c."StartDateUtc") AS "Current"
) fsrdAway ON TRUE

INNER JOIN public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"

INNER JOIN public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
LEFT JOIN LATERAL (
  -- Rank via poll_rank_asof — the single poll-rank definition (see the
  -- PollRankAsofFunction migration): the poll in effect at kickoff,
  -- this team's entry in it, or NULL = honestly unranked.
  SELECT public.poll_rank_asof(fsHome."Id", fsHome."SeasonYear", c."StartDateUtc") AS "Current"
) fsrdHome ON TRUE

WHERE c."StartDateUtc" <= NOW() and ((fsAway."Id" = @FranchiseSeasonId) Or (fsHome."Id" = @FranchiseSeasonId))
ORDER BY c."StartDateUtc", fHome."Slug";

-- select * from public."FranchiseSeason" where "Slug" = 'lsu-tigers'