using System.Text.Json;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis.Lab;

internal static class PlayAnalysisLabMapping
{
    public static PlayAnalysisRunSummaryDto ToSummary(PlayAnalysisRun run) =>
        new(run.Id, run.ContestId, run.CompetitionId, run.PlayCount, run.FactCount, run.DroppedCount,
            run.TemplateVersion, run.CreatedUtc, run.CompletedUtc);
}

public interface IGetPlayAnalysisRunsQueryHandler
{
    Task<Result<List<PlayAnalysisRunSummaryDto>>> ExecuteAsync(Guid contestId, CancellationToken cancellationToken = default);
}

/// <summary>A game's lab runs, newest first.</summary>
public class GetPlayAnalysisRunsQueryHandler : IGetPlayAnalysisRunsQueryHandler
{
    private readonly FootballDataContext _dataContext;

    public GetPlayAnalysisRunsQueryHandler(FootballDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<PlayAnalysisRunSummaryDto>>> ExecuteAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var runs = await _dataContext.PlayAnalysisRuns
            .AsNoTracking()
            .Where(x => x.ContestId == contestId)
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => new PlayAnalysisRunSummaryDto(
                x.Id, x.ContestId, x.CompetitionId, x.PlayCount, x.FactCount, x.DroppedCount,
                x.TemplateVersion, x.CreatedUtc, x.CompletedUtc))
            .ToListAsync(cancellationToken);

        return new Success<List<PlayAnalysisRunSummaryDto>>(runs);
    }
}

public interface IGetPlayAnalysisRunQueryHandler
{
    Task<Result<PlayAnalysisRunDetailDto>> ExecuteAsync(Guid runId, CancellationToken cancellationToken = default);
}

/// <summary>
/// One lab run: every play of the game in order, with the facts the run
/// generated (and their ratings) and the facts it dropped, with reasons.
/// </summary>
public class GetPlayAnalysisRunQueryHandler : IGetPlayAnalysisRunQueryHandler
{
    private readonly FootballDataContext _dataContext;

    public GetPlayAnalysisRunQueryHandler(FootballDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    private record StoredDrop(Guid PlayId, string FactType, string Kind, Guid? AthleteSeasonId, string Reason);

    public async Task<Result<PlayAnalysisRunDetailDto>> ExecuteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _dataContext.PlayAnalysisRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);

        if (run is null)
        {
            return new Failure<PlayAnalysisRunDetailDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(runId), "Lab run not found.")]);
        }

        var plays = await _dataContext.CompetitionPlays
            .AsNoTracking()
            .Where(x => x.CompetitionId == run.CompetitionId)
            .Select(x => new { x.Id, x.SequenceNumber, x.TypeId, x.Text })
            .ToListAsync(cancellationToken);

        var facts = await _dataContext.PlayAnalyses
            .AsNoTracking()
            .Where(x => x.AnalysisRunId == runId)
            .Select(x => new
            {
                x.PlayId,
                Fact = new PlayAnalysisLabFactDto(
                    x.Id,
                    x.Ordinal,
                    x.FactType,
                    x.Text,
                    x.Priority,
                    x.Facts,
                    _dataContext.PlayAnalysisRatings
                        .Where(r => r.PlayAnalysisId == x.Id)
                        .Select(r => (int?)r.Stars)
                        .FirstOrDefault())
            })
            .ToListAsync(cancellationToken);

        var factsByPlay = facts.ToLookup(x => x.PlayId, x => x.Fact);
        var dropsByPlay = (JsonSerializer.Deserialize<List<StoredDrop>>(run.DroppedFacts, JsonOptions) ?? [])
            .ToLookup(x => x.PlayId);

        var detail = plays
            .OrderBy(p => long.TryParse(p.SequenceNumber, out var n) ? n : long.MaxValue)
            .Select(p => new PlayAnalysisLabPlayDto(
                p.Id,
                p.SequenceNumber,
                p.TypeId,
                p.Text,
                factsByPlay[p.Id].OrderBy(f => f.Ordinal).ToList(),
                dropsByPlay[p.Id]
                    .Select(d => new PlayAnalysisLabDropDto(d.FactType, d.Kind, d.AthleteSeasonId, d.Reason))
                    .ToList()))
            .ToList();

        return new Success<PlayAnalysisRunDetailDto>(new PlayAnalysisRunDetailDto(PlayAnalysisLabMapping.ToSummary(run), detail));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
