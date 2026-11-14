using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.FranchiseContestFacts;

/// <summary>
/// The conference record's switch (same rule as EnrichFranchiseSeasonHandler):
/// a season uses ESPN's conferenceCompetition flag only when a real share
/// (10%) of its games carry it. Season-wide, so a season rebuild computes it
/// once and passes it to every contest's projection.
/// </summary>
public static class SeasonConferenceFlag
{
    public static async Task<bool> SeasonUsesFlagAsync(
        FootballDataContext dataContext,
        int seasonYear,
        CancellationToken cancellationToken)
    {
        var seasonCompetitions = dataContext.Competitions
            .AsNoTracking()
            .Where(k => k.Contest.SeasonYear == seasonYear);

        var total = await seasonCompetitions.CountAsync(cancellationToken);
        var flagged = await seasonCompetitions.CountAsync(k => k.IsConferenceCompetition, cancellationToken);

        return flagged > 0 && flagged * 10 >= total;
    }
}
