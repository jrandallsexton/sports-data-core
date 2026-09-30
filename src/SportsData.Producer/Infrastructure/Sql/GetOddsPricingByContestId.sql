-- Odds pricing for one contest: both teams' current moneyline and spread
-- price, plus the over/under prices.
--
-- Same provider row as the league matchup queries (GetMatchupsByContestIds /
-- GetMatchupsBySeasonWeekId): the preferred provider, else the fallback. The
-- prices therefore pair with the spread and total the API's matchup already
-- carries from those queries.
--
-- The odds lateral is correlated to the CONTEST (Competition joined inside
-- it), so the query returns at most one row. No row means the contest does not
-- exist here; a contest with no odds from either provider still returns a row,
-- with NULL prices.
SELECT
  c."Id"                    AS "ContestId",
  away."MoneylineCurrent"   AS "AwayMoneyLine",
  home."MoneylineCurrent"   AS "HomeMoneyLine",
  away."SpreadPriceCurrent" AS "AwaySpreadPrice",
  home."SpreadPriceCurrent" AS "HomeSpreadPrice",
  co."OverOdds"             AS "OverOdds",
  co."UnderOdds"            AS "UnderOdds"
FROM public."Contest" c
LEFT JOIN LATERAL (
  SELECT o."Id", o."OverOdds", o."UnderOdds"
  FROM public."Competition" comp
  INNER JOIN public."CompetitionOdds" o ON o."CompetitionId" = comp."Id"
  WHERE comp."ContestId" = c."Id"
    AND o."ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
  ORDER BY CASE WHEN o."ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE
LEFT JOIN public."CompetitionTeamOdds" away ON away."CompetitionOddsId" = co."Id" AND away."Side" = 'Away'
LEFT JOIN public."CompetitionTeamOdds" home ON home."CompetitionOddsId" = co."Id" AND home."Side" = 'Home'
WHERE c."Id" = @ContestId;
