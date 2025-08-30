﻿using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions;

public class FootballCompetitionBroadcastingJob
{
    private readonly ILogger<FootballCompetitionBroadcastingJob> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly HttpClient _httpClient;

    public FootballCompetitionBroadcastingJob(
        ILogger<FootballCompetitionBroadcastingJob> logger,
        FootballDataContext dataContext,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _dataContext = dataContext;
        _httpClient = httpClientFactory.CreateClient(); // default client
    }

    public async Task ExecuteAsync(BroadcastFootballCompetitionCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            _logger.LogInformation("Broadcasting job started for {@command}", command);

            var competition = await _dataContext.Competitions
                .Include(c => c.ExternalIds)
                .Include(c => c.Competitors)
                .ThenInclude(p => p.ExternalIds)
                .Where(c => c.Id == command.CorrelationId && c.ContestId == command.ContestId)
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
            return json.FromJson<EspnEventCompetitionDto>();
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

    private void StartPollingWorkers(EspnEventCompetitionDto competitionDto, BroadcastFootballCompetitionCommand command)
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

    private async Task PublishDocumentRequestAsync(Uri refUri, DocumentType type, BroadcastFootballCompetitionCommand command)
    {
        await Task.Delay(100);
        _logger.LogDebug("Would publish {Type} document for {Uri}", type, refUri);
        // await _publishEndpoint.Publish(new DocumentRequested(...));
        // await _dataContext.OutboxPings.AddAsync(new OutboxPing());
        // await _dataContext.SaveChangesAsync();
    }
}

public class BroadcastFootballCompetitionCommand
{
    public Guid ContestId { get; set; }
    public Guid CompetitionId { get; set; }
    public Guid CorrelationId { get; init; }
}
