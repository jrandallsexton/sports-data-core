using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions;

/// <summary>
/// Football live-game streamer. Polls Probabilities, Drives, Plays, Situation,
/// and Leaders child docs at football-tuned cadences. All worker lifecycle,
/// status state-machine, and document-request fan-out logic lives in
/// CompetitionStreamerBase.
/// </summary>
public class FootballCompetitionStreamer : CompetitionStreamerBase<EspnFootballEventCompetitionDto>
{
    public FootballCompetitionStreamer(
        ILogger<FootballCompetitionStreamer> logger,
        FootballDataContext dataContext,
        IHttpClientFactory httpClientFactory,
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, httpClientFactory, eventBus, deliveryScope, dateTimeProvider)
    {
    }

    protected override IEnumerable<(Uri? RefUri, DocumentType DocumentType, int IntervalSeconds, bool RequiresParentId)>
        GetPollingTargets(EspnFootballEventCompetitionDto competitionDto)
    {
        // RequiresParentId mirrors what the downstream processor actually reads
        // (audited 2026-05-15). Probability resolves its parent via the DTO's
        // Competition ref; the other four call TryGetOrDeriveParentId.
        yield return (competitionDto.Probabilities?.Ref, DocumentType.EventCompetitionProbability, 15, RequiresParentId: false);
        yield return (competitionDto.Drives?.Ref,        DocumentType.EventCompetitionDrive,       15, RequiresParentId: true);
        yield return (competitionDto.Details?.Ref,       DocumentType.EventCompetitionPlay,        10, RequiresParentId: true);
        yield return (competitionDto.Situation?.Ref,     DocumentType.EventCompetitionSituation,    5, RequiresParentId: true);
        yield return (competitionDto.Leaders?.Ref,       DocumentType.EventCompetitionLeaders,     60, RequiresParentId: true);
    }
}
