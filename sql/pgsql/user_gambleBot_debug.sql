select * from public."User" where "Id" = '82942ecd-7b8d-420f-a13c-7e90d0ecd048'

select * from public."UserPick" where "UserId" = '82942ecd-7b8d-420f-a13c-7e90d0ecd048' and "PickemGroupId" = 'aa7a482f-2204-429a-bb7c-75bc2dfef92b' and "Week" = 15

select * from public."ContestPrediction" where "ContestId" = '32d1426c-eb3b-966e-12c7-af0c3850cb0c' -- UCF v BYU
-- ucf 524ddd90-4cda-1bb4-56e2-e3a6e5f1b846
-- byu f612a671-718a-55cd-60f0-8fde6743e286
select * from public."ContestPrediction" where "ContestId" = '93fb5ebf-69c7-5724-4a49-bf5e59320319' -- Troy v JMU

WITH ranked_picks AS (
    SELECT "Id",
           "UserId",
           "PickemGroupId",
           "ContestId",
           "CreatedUtc",
           ROW_NUMBER() OVER (
               PARTITION BY "UserId", "PickemGroupId", "ContestId"
               ORDER BY "CreatedUtc" DESC
           ) AS rn
    FROM public."UserPick"
    WHERE "UserId" = '82942ecd-7b8d-420f-a13c-7e90d0ecd048'
)
SELECT *
FROM ranked_picks
WHERE rn > 1
ORDER BY "PickemGroupId", "ContestId", rn;