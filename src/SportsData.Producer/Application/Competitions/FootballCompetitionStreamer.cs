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
        IEventBus publishEndpoint)
        : base(logger, dataContext, httpClientFactory, publishEndpoint)
    {
    }

    protected override IEnumerable<(Uri? RefUri, DocumentType DocumentType, int IntervalSeconds)>
        GetPollingTargets(EspnFootballEventCompetitionDto competitionDto)
    {
        yield return (competitionDto.Probabilities?.Ref, DocumentType.EventCompetitionProbability, 15);
        yield return (competitionDto.Drives?.Ref, DocumentType.EventCompetitionDrive, 15);
        yield return (competitionDto.Details?.Ref, DocumentType.EventCompetitionPlay, 10);
        yield return (competitionDto.Situation?.Ref, DocumentType.EventCompetitionSituation, 5);
        yield return (competitionDto.Leaders?.Ref, DocumentType.EventCompetitionLeaders, 60);
    }
}
