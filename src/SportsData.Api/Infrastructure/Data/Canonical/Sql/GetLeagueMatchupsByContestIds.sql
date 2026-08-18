SELECT
  c."SeasonWeekId" as "SeasonWeekId",
  c."Id" AS "ContestId",
  c."StartDateUtc" as "StartDateUtc",
  replace(cs."StatusDescription", ' ', '') AS "Status",

  STRING_AGG(cb."MediaName", ' | ') AS "Broadcasts",

  v."Name" as "Venue",
  v."City" as "VenueCity",
  v."State" as "VenueState",
  
  fAway."DisplayName" as "Away",
  fAway."Abbreviation" as "AwayShort",
  fsAway."Id" as "AwayFranchiseSeasonId",
  flAway."Uri" as "AwayLogoUri",
  fAway."Slug" as "AwaySlug",
  fAway."ColorCodeHex" as "AwayColor",
  fsrdAway."Current" as "AwayRank",
  gsAway."Slug" as "AwayConferenceSlug",
  fsAway."Wins" as "AwayWins",
  fsAway."Losses" as "AwayLosses",
  fsAway."ConferenceWins" as "AwayConferenceWins",
  fsAway."ConferenceLosses" as "AwayConferenceLosses",
  
  fHome."DisplayName" as "Home",
  fHome."Abbreviation" as "HomeShort",
  fsHome."Id" as "HomeFranchiseSeasonId",
  flHome."Uri" as "HomeLogoUri",  
  fHome."Slug" as "HomeSlug",
  fHome."ColorCodeHex" as "HomeColor",
  fsrdHome."Current" as "HomeRank",
  gsHome."Slug" as "HomeConferenceSlug",
  fsHome."Wins" as "HomeWins",
  fsHome."Losses" as "HomeLosses",
  fsHome."ConferenceWins" as "HomeConferenceWins",
  fsHome."ConferenceLosses" as "HomeConferenceLosses",
  
  co."Details" as "SpreadCurrentDetails",
  co."Spread" as "SpreadCurrent",
  cto."SpreadPointsOpen" as "SpreadOpen",
  co."OverUnder" as "OverUnderCurrent",
  co."TotalPointsOpen" as "OverUnderOpen",
  co."OverOdds" as "OverOdds",
  co."UnderOdds" as "UnderOdds",

  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseSeasonId",
  c."OverUnder" as "OverUnderResult",
  c."EndDateUtc" as "CompletedUtc"

FROM public."Contest" c
INNER JOIN public."Venue" v on v."Id" = c."VenueId"
INNER JOIN public."Competition" comp on comp."ContestId" = c."Id"
LEFT JOIN public."CompetitionBroadcast" cb on cb."CompetitionId" = comp."Id"
LEFT  JOIN public."CompetitionStatus" cs on cs."CompetitionId" = comp."Id"

-- Use LATERAL join to prioritize ESPN (58) over DraftKings (100)
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" 
    AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE

LEFT  JOIN public."CompetitionTeamOdds" cto on cto."CompetitionOddsId" = co."Id" and cto."Side" = 'Home'

INNER JOIN public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"

LEFT JOIN LATERAL (
  SELECT fl.*
  FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fAway."Id"
  ORDER BY fl."CreatedUtc" ASC -- or ORDER BY fl."Id" ASC
  LIMIT 1
) flAway ON TRUE

INNER JOIN public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"

LEFT JOIN LATERAL (
  -- Rank from the SeasonPoll store (the store the weekly rankings job
  -- feeds). POLL-FIRST: find THE poll in effect (latest published before kickoff), then this
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
        AND spw."DateUtc" <= c."StartDateUtc"
      ORDER BY spw."DateUtc" DESC, CASE WHEN spw."Type" = 'cfp' THEN 0 ELSE 1 END
      LIMIT 1)
    AND spwe."FranchiseSeasonId" = fsAway."Id"
    AND NOT spwe."IsOtherReceivingVotes" AND NOT spwe."IsDroppedOut"
  LIMIT 1
) fsrdAway ON TRUE

INNER JOIN public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"

LEFT JOIN LATERAL (
  SELECT fl.*
  FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fHome."Id"
  ORDER BY fl."CreatedUtc" ASC -- or ORDER BY fl."Id" ASC
  LIMIT 1
) flHome ON TRUE

INNER JOIN public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
LEFT JOIN LATERAL (
  -- Rank from the SeasonPoll store (the store the weekly rankings job
  -- feeds). POLL-FIRST: find THE poll in effect (latest published before kickoff), then this
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
        AND spw."DateUtc" <= c."StartDateUtc"
      ORDER BY spw."DateUtc" DESC, CASE WHEN spw."Type" = 'cfp' THEN 0 ELSE 1 END
      LIMIT 1)
    AND spwe."FranchiseSeasonId" = fsHome."Id"
    AND NOT spwe."IsOtherReceivingVotes" AND NOT spwe."IsDroppedOut"
  LIMIT 1
) fsrdHome ON TRUE

WHERE c."Id" = ANY(@ContestIds)

GROUP BY
  c."SeasonWeekId",
  c."Id",
  c."StartDateUtc",
  cs."StatusDescription",
  v."Name", v."City", v."State",

  fAway."DisplayName", fAway."DisplayNameShort", fsAway."Id", flAway."Uri", fAway."Slug",
  fsrdAway."Current", gsAway."Slug",
  fsAway."Wins", fsAway."Losses", fsAway."ConferenceWins", fsAway."ConferenceLosses",

    fAway."Abbreviation", fAway."ColorCodeHex",
  fHome."Abbreviation", fHome."ColorCodeHex",


  fHome."DisplayName", fHome."DisplayNameShort", fsHome."Id", flHome."Uri", fHome."Slug",
  fsrdHome."Current", gsHome."Slug",
  fsHome."Wins", fsHome."Losses", fsHome."ConferenceWins", fsHome."ConferenceLosses",

  co."Details", co."Spread", co."OverUnder", co."OverOdds", co."UnderOdds",
  cto."SpreadPointsOpen", co."TotalPointsOpen",
  c."AwayScore", c."HomeScore", c."WinnerFranchiseSeasonId", c."SpreadWinnerFranchiseSeasonId",
  c."OverUnder", c."EndDateUtc"


ORDER BY c."StartDateUtc", fHome."Slug";
