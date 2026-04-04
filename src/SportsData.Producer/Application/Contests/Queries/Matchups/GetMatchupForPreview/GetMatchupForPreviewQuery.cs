using System;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupForPreview;

public record GetMatchupForPreviewQuery(Guid ContestId);

public record GetMatchupsForPreviewBatchQuery(Guid[] ContestIds);
