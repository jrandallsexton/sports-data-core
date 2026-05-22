using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball;

namespace SportsData.Producer.Application.Competitions;

/// <summary>
/// Baseball (MLB) live-game streamer. Polls Probabilities, Plays, Situation,
/// and Leaders child docs at football-derived cadences (worth tuning once we
/// observe MLB pitch-rate behavior in production). No Drives — that's a
/// football-only concept. All worker lifecycle, status state-machine, and
/// document-request fan-out logic lives in CompetitionStreamerBase.
/// </summary>
public class BaseballCompetitionStreamer : CompetitionStreamerBase<EspnBaseballEventCompetitionDto>
{
    public BaseballCompetitionStreamer(
        ILogger<BaseballCompetitionStreamer> logger,
        BaseballDataContext dataContext,
        IHttpClientFactory httpClientFactory,
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, httpClientFactory, eventBus, deliveryScope, dateTimeProvider)
    {
    }

    protected override IEnumerable<(Uri? RefUri, DocumentType DocumentType, int IntervalSeconds, bool RequiresParentId)>
        GetPollingTargets(EspnBaseballEventCompetitionDto competitionDto)
    {
        // RequiresParentId mirrors what the downstream processor actually reads
        // (audited 2026-05-15). Probability resolves its parent via the DTO's
        // Competition ref; the other three call TryGetOrDeriveParentId.
        yield return (competitionDto.Probabilities?.Ref, DocumentType.EventCompetitionProbability, 60, RequiresParentId: false);
        yield return (competitionDto.Details?.Ref,       DocumentType.EventCompetitionPlay,        30, RequiresParentId: true);
        yield return (competitionDto.Situation?.Ref,     DocumentType.EventCompetitionSituation,   30, RequiresParentId: true);
        yield return (competitionDto.Leaders?.Ref,       DocumentType.EventCompetitionLeaders,     60, RequiresParentId: true);
    }
}
