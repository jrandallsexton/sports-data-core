using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Scoring;

public interface IPickScoringService
{
    void ScorePick(
        PickemGroup group,
        PickemGroupMatchup matchup,
        PickemGroupUserPick pick,
        MatchupResult result);
}