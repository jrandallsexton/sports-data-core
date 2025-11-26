using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record ContestRecapArticlePublished(
        Guid ContestId,
        Guid ArticleId,
        string Title,
        Guid CorrelationId,
        Guid CausationId) : EventBase(CorrelationId, CausationId);
}
