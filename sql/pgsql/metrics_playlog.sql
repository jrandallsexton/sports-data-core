    select
        cp."SequenceNumber" as "Ordinal",
        cp."PeriodNumber" as "Quarter",
        f."Name" as "Team",
        cp."DriveId" as "Drive",
        cp."StartDown" as "Down",
        cp."StartDistance" as "ToGo",
        cp."EndYardsToEndzone" as "YardsToEndzone",
        cp."StartYardLine" as "YardLine",
        cp."EndYardLine" as "EndYardLine",
        pt."Name" as "PlayType",
        cp."Text" as "Description",
        cp."StatYardage" as "Yards",
        cp."ClockDisplayValue" as "TimeRemaining",
        cp."ScoringPlay" as "IsScoringPlay",
        cp."Priority" as "IsKeyPlay"
    from public."CompetitionPlay" cp
    left join public."lkPlayType" pt on pt."Id" = cp."Type"
    inner join public."Competition" co on co."Id" = cp."CompetitionId"
    inner join public."Contest" c on c."Id" = co."ContestId"
    inner join public."FranchiseSeason" fs on fs."Id" = cp."StartTeamFranchiseSeasonId"
    inner join public."Franchise" f on f."Id" = fs."FranchiseId"
    where co."ContestId" = '37b87b09-3599-2e50-1f49-790d7d3c69d5'
    order by cp."SequenceNumber"


    select * from public."CompetitionPlay"
    where "CompetitionId" = '37b87b09-3599-2e50-1f49-790d7d3c69d5' and "StartTeamFranchiseSeasonId" != "TeamFranchiseSeasonId"
    and "Type" != 12 and "Type" != 52 and "Type" != 53
    order by "SequenceNumber"

    

--     WITH base AS (
--   SELECT
--     co."ContestId",
--     f."Name"                        AS "Team",
--     cp."SequenceNumber"             AS "Ordinal",
--     pt."Name"                       AS "PlayType",
--     cp."StartDown"                  AS "Down",
--     cp."StartDistance"              AS "ToGo",
--     cp."StartYardLine",
--     cp."EndYardLine",
--     COALESCE(cp."StartYardsToEndzone", 100 - cp."StartYardLine")::int AS start_yte,
--     COALESCE(cp."EndYardsToEndzone",   100 - cp."EndYardLine")::int   AS end_yte,
--     cp."StatYardage"                AS stat_yds
--   FROM public."CompetitionPlay" cp
--   LEFT JOIN public."lkPlayType" pt       ON pt."Id" = cp."Type"
--   JOIN public."Competition" co           ON co."Id" = cp."CompetitionId"
--   JOIN public."Contest" c                ON c."Id" = co."ContestId"
--   JOIN public."FranchiseSeason" fs       ON fs."Id" = cp."StartTeamFranchiseSeasonId"
--   JOIN public."Franchise" f              ON f."Id" = fs."FranchiseId"
--   WHERE co."ContestId" = 'b6cde160-f48d-9d51-784b-56bf4adb990a'
-- ),
-- -- Keep true from-scrimmage plays only (edit list to your lkPlayType taxonomy as needed)
-- scrimmage AS (
--   SELECT *,
--          (start_yte - end_yte)                    AS delta_yte,          -- corrected yards
--          GREATEST(0, "ToGo")                      AS to_go_nonneg
--   FROM base
--   WHERE "PlayType" IN
--     ('rush','rushAttempt','passReception','passComplete','passIncomplete','sack','scramble','kneel','spike')
-- ),
-- -- Success definition: 7/4/2 rule by down using corrected yards
-- labeled AS (
--   SELECT
--     "Team",
--     "Down",
--     to_go_nonneg,
--     delta_yte,
--     (CASE
--        WHEN "Down" = 1 AND delta_yte >= 7 THEN 1
--        WHEN "Down" = 2 AND delta_yte >= 4 THEN 1
--        WHEN "Down" IN (3,4) AND delta_yte >= to_go_nonneg THEN 1
--        ELSE 0
--      END) AS is_success,
--     (CASE WHEN delta_yte >= 20 THEN 1 ELSE 0 END) AS is_explosive,
--     (CASE WHEN "Down" IN (3,4) THEN 1 ELSE 0 END) AS is_thirdfourth_att,
--     (CASE WHEN "Down" IN (3,4) AND delta_yte >= to_go_nonneg THEN 1 ELSE 0 END) AS is_thirdfourth_conv
--   FROM scrimmage
-- )
-- SELECT
--   "Team",
--   COUNT(*)                                        AS plays,
--   SUM(delta_yte)                                  AS total_yards_corrected,
--   ROUND(AVG(delta_yte)::numeric, 3)               AS ypp,
--   ROUND(AVG(is_success)::numeric, 3)              AS success_rate,
--   ROUND(AVG(is_explosive)::numeric, 3)            AS explosive_rate,
--   SUM(is_thirdfourth_att)                         AS third_fourth_att,
--   SUM(is_thirdfourth_conv)                        AS third_fourth_conv,
--   CASE WHEN SUM(is_thirdfourth_att) > 0
--        THEN ROUND( (SUM(is_thirdfourth_conv)::numeric / SUM(is_thirdfourth_att)) , 3)
--        ELSE NULL
--   END                                             AS third_fourth_conv_rate
-- FROM labeled
-- GROUP BY "Team"
-- ORDER BY "Team";

-- ========= Success Rate by Down =========
-- WITH base AS (
--   SELECT
--     co."ContestId",
--     f."Name"                        AS "Team",
--     pt."Name"                       AS "PlayType",
--     cp."StartDown"                  AS "Down",
--     cp."StartDistance"              AS "ToGo",
--     COALESCE(cp."StartYardsToEndzone", 100 - cp."StartYardLine")::int AS start_yte,
--     COALESCE(cp."EndYardsToEndzone",   100 - cp."EndYardLine")::int   AS end_yte
--   FROM public."CompetitionPlay" cp
--   LEFT JOIN public."lkPlayType" pt       ON pt."Id" = cp."Type"
--   JOIN public."Competition" co           ON co."Id" = cp."CompetitionId"
--   JOIN public."Contest" c                ON c."Id" = co."ContestId"
--   JOIN public."FranchiseSeason" fs       ON fs."Id" = cp."StartTeamFranchiseSeasonId"
--   JOIN public."Franchise" f              ON f."Id" = fs."FranchiseId"
--   WHERE co."ContestId" = 'b6cde160-f48d-9d51-784b-56bf4adb990a'
-- ),
-- scrimmage AS (
--   SELECT *,
--          (start_yte - end_yte) AS delta_yte,
--          GREATEST(0, "ToGo")   AS to_go_nonneg
--   FROM base
--   WHERE "PlayType" IN
--     ('rush','rushAttempt','passReception','passComplete','passIncomplete','sack','scramble','kneel','spike')
-- )
-- SELECT
--   "Team",
--   "Down",
--   COUNT(*)                               AS plays,
--   ROUND(AVG(delta_yte)::numeric, 3)      AS ypp_on_down,
--   ROUND(AVG(
--     CASE
--       WHEN "Down" = 1 AND delta_yte >= 7 THEN 1
--       WHEN "Down" = 2 AND delta_yte >= 4 THEN 1
--       WHEN "Down" IN (3,4) AND delta_yte >= to_go_nonneg THEN 1
--       ELSE 0
--     END
--   )::numeric, 3)                          AS success_rate_on_down
-- FROM scrimmage
-- GROUP BY "Team","Down"
-- ORDER BY "Team","Down";
