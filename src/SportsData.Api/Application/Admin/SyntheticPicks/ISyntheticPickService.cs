using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.Admin.SyntheticPicks;

public interface ISyntheticPickService
{
    /// <summary>
    /// Generates metric-based picks for a synthetic user for a specific pickem group and week.
    /// Applies pick style thresholds to determine whether to follow model predictions or flip picks.
    /// </summary>
    /// <param name="pickemGroupId">The pickem group ID</param>
    /// <param name="pickemGroupPickType">The pick type for the group (SU or ATS)</param>
    /// <param name="syntheticId">The synthetic user ID</param>
    /// <param name="syntheticPickStyle">The pick style to apply (e.g., "moderate", "conservative", "aggressive")</param>
    /// <param name="seasonWeekNumber">The week number to generate picks for</param>
    Task GenerateMetricBasedPicksForSynthetic(
        Guid pickemGroupId,
        PickType pickemGroupPickType,
        Guid syntheticId,
        string syntheticPickStyle,
        int seasonWeekNumber);
}
