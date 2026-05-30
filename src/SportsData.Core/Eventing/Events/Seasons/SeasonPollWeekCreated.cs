using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Seasons
{
    /// <summary>
    /// Fires when a Producer-side <c>SeasonTypeWeekRankingsDocumentProcessor</c>
    /// creates a new <c>SeasonPollWeek</c> row — i.e. the rankings for a
    /// specific (poll, week) just landed in the canonical DB. Drives the
    /// API-side rank-poll refresh path: leagues with a <c>RankingFilter</c>
    /// whose window overlaps the affected SeasonWeek re-fire matchup
    /// generation so newly-ranked contests get included.
    ///
    /// <para>
    /// <c>SeasonWeekId</c> is nullable because preseason / postseason
    /// "headline" polls (e.g. final AP poll of a season) don't map to a
    /// specific pickable SeasonWeek. The API consumer skips those.
    /// </para>
    ///
    /// <para>
    /// SeasonWeek bounds are inlined on the payload so the API consumer
    /// doesn't have to call back to Producer to resolve them — this is the
    /// hot path on poll publication and a tight inner loop matters.
    /// </para>
    /// </summary>
    public record SeasonPollWeekCreated(
        Guid SeasonPollWeekId,
        Guid SeasonPollId,
        Guid? SeasonWeekId,
        DateTime? SeasonWeekStartDate,
        DateTime? SeasonWeekEndDate,
        int? SeasonYear,
        string? PollSlug,
        Uri? Ref,
        Sport Sport,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
