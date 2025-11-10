using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions;

public interface IFootballCompetitionBroadcastingJob
{
    Task ExecuteAsync(StreamFootballCompetitionCommand command);
}

/// <summary>
/// this class is responsible for live-action updates for canonical data
/// and broadcasting those changes to downstream systems
/// </summary>
public class FootballCompetitionStreamer : IFootballCompetitionBroadcastingJob
{
    private readonly ILogger<FootballCompetitionStreamer> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly HttpClient _httpClient;
    private readonly IEventBus _publishEndpoint;

    public FootballCompetitionStreamer(
        ILogger<FootballCompetitionStreamer> logger,
        FootballDataContext dataContext,
        IHttpClientFactory httpClientFactory,
        IEventBus publishEndpoint
        )
    {
        _logger = logger;
        _dataContext = dataContext;
        _httpClient = httpClientFactory.CreateClient(); // default client
        _publishEndpoint = publishEndpoint;
    }

    public async Task ExecuteAsync(StreamFootballCompetitionCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            _logger.LogInformation("Broadcasting job started for {@command}", command);

            var competition = await _dataContext.Competitions
                .Include(c => c.Contest)
                .Include(c => c.ExternalIds)
                .Include(c => c.Competitors)
                .ThenInclude(p => p.ExternalIds)
                .Where(c => c.Id == command.CompetitionId && c.Contest.IsFinal == false)
                .AsSplitQuery()
                .FirstOrDefaultAsync();

            if (competition == null)
            {
                _logger.LogError("Competition not found");
                return;
            }

            var externalId = competition.ExternalIds.FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);
            if (externalId == null)
            {
                _logger.LogError("CompetitionExternalId not found");
                return;
            }

            // we also need the Competition document because it has the URLs we need for spawned processes
            var competitionDto = await GetCompetition(new Uri(externalId.SourceUrl));
            if (competitionDto == null)
            {
                _logger.LogError("Competition fetch failed");
                return;
            }

            var statusUri = EspnUriMapper
                .CompetitionRefToCompetitionStatusRef(new Uri(externalId.SourceUrl));

            var status = await GetStatusAsync(statusUri);
            if (status == null)
            {
                _logger.LogError("Initial status fetch failed");
                return;
            }

            switch (status.Type.Name)
            {
                case "STATUS_SCHEDULED":
                    await WaitForKickoffAsync(statusUri);
                    break;
                case "STATUS_IN_PROGRESS":
                    _logger.LogInformation("Kickoff detected. Starting polling workers...");
                    break;
                case "STATUS_FINAL":
                    _logger.LogInformation("Competition already final. Skipping polling.");
                    break;
            }

            _logger.LogInformation("Kickoff confirmed. Starting polling workers...");

            StartPollingWorkers(competitionDto, command);

            await PollWhileInProgressAsync(statusUri);
        }
    }

    private async Task<EspnEventCompetitionDto?> GetCompetition(Uri uri)
    {
        try
        {
            var response = await _httpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var result = json.FromJson<EspnEventCompetitionDto>();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status from {Uri}", uri);
            return null;
        }
    }

    private async Task<EspnEventCompetitionStatusDto?> GetStatusAsync(Uri uri)
    {
        try
        {
            var response = await _httpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return json.FromJson<EspnEventCompetitionStatusDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status from {Uri}", uri);
            return null;
        }
    }

    private async Task WaitForKickoffAsync(Uri statusUri)
    {
        _logger.LogInformation("Competition is scheduled. Polling for kickoff...");

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(20));
            var status = await GetStatusAsync(statusUri);

            if (status?.Type.Name == "STATUS_IN_PROGRESS")
            {
                _logger.LogInformation("Kickoff detected");
                return;
            }

            if (status?.Type.Name == "STATUS_FINAL")
            {
                _logger.LogWarning("Game marked final before starting? Exiting.");
                return;
            }
        }
    }

    private void StartPollingWorkers(EspnEventCompetitionDto competitionDto, StreamFootballCompetitionCommand command)
    {
        var refs = new[]
        {
            (competitionDto.Probabilities.Ref, DocumentType.EventCompetitionProbability, 15),
            (competitionDto.Drives.Ref, DocumentType.EventCompetitionDrive, 15),
            (competitionDto.Details.Ref, DocumentType.EventCompetitionPlay, 10),
            (competitionDto.Situation.Ref, DocumentType.EventCompetitionSituation, 5),
            (competitionDto.Leaders.Ref, DocumentType.EventCompetitionLeaders, 60)
        };

        foreach (var (refUri, docType, intervalSeconds) in refs)
        {
            SpawnPollingWorker(() =>
                PublishDocumentRequestAsync(refUri, docType, command), intervalSeconds);
        }
    }

    private async Task PollWhileInProgressAsync(Uri statusUri)
    {
        _logger.LogInformation("Polling status while game is in progress...");

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));

            var status = await GetStatusAsync(statusUri);

            if (status?.Type.Name == "STATUS_FINAL")
            {
                _logger.LogInformation("Game has ended.");
                return;
            }

            // Optionally update internal status in DB
        }
    }

    private void SpawnPollingWorker(Func<Task> taskFactory, int intervalSeconds)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await taskFactory();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Polling worker failed during execution.");
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
            }
        });
    }

    private async Task PublishDocumentRequestAsync(
        Uri refUri,
        DocumentType type,
        StreamFootballCompetitionCommand command)
    {
        var parentId = type is
            DocumentType.EventCompetitionProbability or
            DocumentType.EventCompetitionDrive or
            DocumentType.EventCompetitionSituation
            ? command.CompetitionId.ToString()
            : null;

        _logger.LogDebug("publish {Type} document for {Uri}", type, refUri);
        await _publishEndpoint.Publish(new DocumentRequested (
            Id: Guid.NewGuid().ToString(),
            ParentId: parentId,
            Uri: refUri,
            Sport: Sport.FootballNcaa,
            SeasonYear: 2025,
            DocumentType: type,
            SourceDataProvider : SourceDataProvider.Espn,
            CorrelationId: command.CorrelationId,
            CausationId: command.CorrelationId
            ));
        await _dataContext.OutboxPings.AddAsync(new OutboxPing());
        await _dataContext.SaveChangesAsync();
    }
}