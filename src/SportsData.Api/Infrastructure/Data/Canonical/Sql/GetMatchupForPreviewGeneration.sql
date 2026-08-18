  SELECT
    c."Sport" as "Sport",
	sp."Year" as "SeasonYear",
	sw."Number" as "WeekNumber",
	c."Id" AS "ContestId",
    cn."Headline" AS "Headline",
	c."StartDateUtc" as "StartDateUtc",
    cs."StatusTypeName" as "Status",
    cs."StatusDescription" as "StatusDescription",

	v."Name" as "Venue",
	v."City" as "VenueCity",
	v."State" as "VenueState",

	fsAway."Id" as "AwayFranchiseSeasonId",
	fAway."DisplayName" as "Away",
	fAway."Slug" as "AwaySlug",
	fsrdAway."Current" as "AwayRank",
	gsAway."Id" as "AwayConferenceId",
	gsAway."Slug" as "AwayConferenceSlug",
	gsAwayParent."Slug" as "AwayParentConferenceSlug",
	fsAway."Wins" as "AwayWins",
	fsAway."Losses" as "AwayLosses",
	fsAway."ConferenceWins" as "AwayConferenceWins",
	fsAway."ConferenceLosses" as "AwayConferenceLosses",

	fsHome."Id" as "HomeFranchiseSeasonId",
	fHome."DisplayName" as "Home",
	fHome."Slug" as "HomeSlug",
	fsrdHome."Current" as "HomeRank",
	gsHome."Id" as "HomeConferenceId",
	gsHome."Slug" as "HomeConferenceSlug",
	gsHomeParent."Slug" as "HomeParentConferenceSlug",
	fsHome."Wins" as "HomeWins",
	fsHome."Losses" as "HomeLosses",
	fsHome."ConferenceWins" as "HomeConferenceWins",
	fsHome."ConferenceLosses" as "HomeConferenceLosses",

	co."Details" as "Spread",
	(co."Spread" * -1) as "AwaySpread",
	co."Spread" as "HomeSpread",
	co."OverUnder" as "OverUnder",
	co."OverOdds" as "OverOdds",
	co."UnderOdds" as "UnderOdds"
  FROM public."Contest" c
  inner join public."SeasonPhase" sp on sp."Id" = c."SeasonPhaseId"
  inner join public."SeasonWeek" sw on sw."Id" = c."SeasonWeekId"
  inner join public."Venue" v on v."Id" = c."VenueId"
  inner join public."Competition" comp on comp."ContestId" = c."Id"
  inner join public."CompetitionStatus" cs on cs."CompetitionId" = comp."Id"
  left join public."CompetitionNote" cn on cn."CompetitionId" = comp."Id" and cn."Type" = 'event'
  
  -- Use LATERAL join to prioritize ESPN (58) over DraftKings (100)
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" 
    AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE

  inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
  inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
  inner join public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
  inner join public."GroupSeason" gsAwayParent on gsAway."ParentId" = gsAwayParent."Id"
  inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
  inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
  inner join public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
  inner join public."GroupSeason" gsHomeParent on gsHome."ParentId" = gsHomeParent."Id"

  LEFT JOIN LATERAL (
  -- Rank via poll_rank_asof — the single poll-rank definition (see the
  -- PollRankAsofFunction migration): the poll in effect at kickoff,
  -- this team's entry in it, or NULL = honestly unranked.
  SELECT public.poll_rank_asof(fsAway."Id", fsAway."SeasonYear", c."StartDateUtc") AS "Current"
) fsrdAway ON TRUE

  LEFT JOIN LATERAL (
  -- Rank via poll_rank_asof — the single poll-rank definition (see the
  -- PollRankAsofFunction migration): the poll in effect at kickoff,
  -- this team's entry in it, or NULL = honestly unranked.
  SELECT public.poll_rank_asof(fsHome."Id", fsHome."SeasonYear", c."StartDateUtc") AS "Current"
) fsrdHome ON TRUE
where c."Id" = @ContestId