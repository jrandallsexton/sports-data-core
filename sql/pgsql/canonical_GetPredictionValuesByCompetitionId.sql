-- select * from public."CompetitionPrediction"
-- select * from public."CompetitionPredictionValue"
-- select * from public."PredictionMetric" order by "Name"

select
    cp."FranchiseSeasonId",
    cp."IsHome",
    pm."Name" as "PredictionMetricName",
    cpv."Value" as "PredictionValue"
from public."CompetitionPrediction" cp
inner join public."CompetitionPredictionValue" cpv on cpv."CompetitionPredictionId" = cp."Id"
inner join public."PredictionMetric" pm on pm."Id" = cpv."PredictionMetricId"
where cp."CompetitionId" = 'd8c706a6-bdeb-e534-2063-56e9083c5bf8'
order by cp."IsHome" desc, pm."Name";