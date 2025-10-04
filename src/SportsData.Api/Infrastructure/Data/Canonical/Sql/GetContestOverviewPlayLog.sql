    select
        cp."SequenceNumber" as "Ordinal",
        cp."PeriodNumber" as "Quarter",
        f."Name" as "Team",
        cp."Text" as "Description",
        cp."ClockDisplayValue" as "TimeRemaining",
        cp."ScoringPlay" as "IsScoringPlay",
        cp."Priority" as "IsKeyPlay"
    from public."CompetitionPlay" cp
    inner join public."Competition" co on co."Id" = cp."CompetitionId"
    inner join public."Contest" c on c."Id" = co."ContestId"
    inner join public."FranchiseSeason" fs on fs."Id" = cp."StartTeamFranchiseSeasonId"
    inner join public."Franchise" f on f."Id" = fs."FranchiseId"
    where co."ContestId" = @ContestId
    order by cp."SequenceNumber"