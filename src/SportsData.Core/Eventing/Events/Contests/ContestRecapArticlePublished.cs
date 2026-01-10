using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record ContestRecapArticlePublished(
        Guid ContestId,
        Guid ArticleId,
        string Title,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
