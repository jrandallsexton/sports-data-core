using System.Text.Json;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis.Lab;

public interface IRunPlayAnalysisLabCommandHandler
{
    Task<Result<PlayAnalysisRunSummaryDto>> ExecuteAsync(
        Guid contestId,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Replays a game's stored plays through the instant-analysis pipeline as a new
/// lab run (task 09). Synchronous, like the admin re-enrich command: a game is
/// a few hundred plays and only the classified ones do real work.
///
/// Every play is analysed in sequence order with the run's id (so lab rows
/// never mix with live ones, and two runs never collide) and with
/// <see cref="SeasonDocumentRule.BeforeKickoff"/>: season documents are
/// rewritten in place, so one written after kickoff already includes the game.
/// The per-game flag is bypassed (a completed game may have no stream row).
/// Dropped facts are stored on the run, since live drops only reach the logs.
/// </summary>
public class RunPlayAnalysisLabCommandHandler : IRunPlayAnalysisLabCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<RunPlayAnalysisLabCommandHandler> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IPlayAnalyser _analyser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RunPlayAnalysisLabCommandHandler(
        ILogger<RunPlayAnalysisLabCommandHandler> logger,
        FootballDataContext dataContext,
        IPlayAnalyser analyser,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _analyser = analyser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<PlayAnalysisRunSummaryDto>> ExecuteAsync(
        Guid contestId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ContestId"] = contestId,
            ["CorrelationId"] = correlationId
        });

        var competitionId = await _dataContext.Competitions
            .AsNoTracking()
            .Where(x => x.ContestId == contestId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (competitionId is null)
        {
            return new Failure<PlayAnalysisRunSummaryDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(contestId), "No competition found for this contest.")]);
        }

        var plays = await _dataContext.CompetitionPlays
            .AsNoTracking()
            .Where(x => x.CompetitionId == competitionId)
            .Select(x => new { x.Id, x.SequenceNumber })
            .ToListAsync(cancellationToken);

        if (plays.Count == 0)
        {
            return new Failure<PlayAnalysisRunSummaryDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(contestId), "This game has no stored plays to replay.")]);
        }

        var run = new PlayAnalysisRun
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            CompetitionId = competitionId.Value,
            TemplateVersion = FactPhraser.TemplateVersion,
            CreatedUtc = _dateTimeProvider.UtcNow(),
            CreatedBy = correlationId
        };

        _dataContext.PlayAnalysisRuns.Add(run);
        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Instant analysis lab run started. AnalysisRunId={AnalysisRunId}, Plays={PlayCount}", run.Id, plays.Count);

        var facts = 0;
        var dropped = new List<object>();

        foreach (var play in plays.OrderBy(p => long.TryParse(p.SequenceNumber, out var n) ? n : long.MaxValue))
        {
            var outcome = await _analyser.Analyse(play.Id, contestId, correlationId, run.Id, SeasonDocumentRule.BeforeKickoff);

            facts += outcome.Persisted;
            dropped.AddRange(outcome.Dropped.Select(d => new
            {
                playId = play.Id,
                factType = d.FactType,
                kind = d.Kind.ToString(),
                athleteSeasonId = d.AthleteSeasonId,
                reason = d.Reason
            }));
        }

        run.PlayCount = plays.Count;
        run.FactCount = facts;
        run.DroppedCount = dropped.Count;
        run.DroppedFacts = JsonSerializer.Serialize(dropped, JsonOptions);
        run.CompletedUtc = _dateTimeProvider.UtcNow();
        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Instant analysis lab run completed. AnalysisRunId={AnalysisRunId}, Plays={PlayCount}, Facts={FactCount}, Dropped={DroppedCount}",
            run.Id, run.PlayCount, run.FactCount, run.DroppedCount);

        return new Success<PlayAnalysisRunSummaryDto>(PlayAnalysisLabMapping.ToSummary(run));
    }
}
