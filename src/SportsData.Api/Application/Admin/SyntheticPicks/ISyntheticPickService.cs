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
    /// <summary>
        /// Generate metric-based picks for a synthetic user for a specific pickem group and week using the given pick style and group pick type.
        /// </summary>
        /// <param name="pickemGroupId">ID of the pickem group to generate picks for.</param>
        /// <param name="pickemGroupPickType">Pick type for the group (e.g., SU or ATS) that determines how picks are interpreted.</param>
        /// <param name="syntheticId">ID of the synthetic user receiving the generated picks.</param>
        /// <param name="syntheticPickStyle">Pick style to apply (e.g., "moderate", "conservative", "aggressive") which influences threshold behavior.</param>
        /// <param name="seasonWeekNumber">Week number in the season for which to generate picks.</param>
        /// <param name="cancellationToken">Token to observe for cooperative cancellation.</param>
    Task GenerateMetricBasedPicksForSynthetic(
        Guid pickemGroupId,
        PickType pickemGroupPickType,
        Guid syntheticId,
        string syntheticPickStyle,
        int seasonWeekNumber,
        CancellationToken cancellationToken = default);
}