using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Scoring;

public interface IPickScoringService
{
    void ScorePick(
        PickemGroup group,
        double? spread,
        PickemGroupUserPick pick,
        MatchupResult result);
}