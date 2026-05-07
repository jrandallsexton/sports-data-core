using System.Globalization;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetition)]
public class BaseballEventCompetitionDocumentProcessor<TDataContext> : EventCompetitionDocumentProcessorBase<TDataContext>
    where TDataContext : BaseballDataContext
{
    public BaseballEventCompetitionDocumentProcessor(
        ILogger<BaseballEventCompetitionDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override EspnEventCompetitionDtoBase? DeserializeDto(string document)
        => document.FromJson<EspnBaseballEventCompetitionDto>();

    protected override CompetitionBase CreateEntity(
        EspnEventCompetitionDtoBase dto,
        IGenerateExternalRefIdentities identityGenerator,
        Guid contestId,
        Guid correlationId)
    {
        return dto.AsBaseballEntity(identityGenerator, contestId, correlationId);
    }

    // Inline series ingestion. ESPN ships current-series and season-series
    // state inline on the competition payload (no $ref). Both are persisted
    // as canonical entities; identity for current-series hashes ESPN's
    // seriesId, identity for season-series synthesizes from
    // (SeasonYear, sorted FranchiseSeasonId pair). Writes are staged on the
    // change tracker; the base class's tail SaveChangesAsync commits them
    // together with the competition update and any outbox publishes.
    //
    // See docs/mlb-series-ingestion-plan.md.
    protected override async Task ProcessSportSpecificCompetitionData(
        ProcessDocumentCommand command,
        EspnEventCompetitionDtoBase dto,
        CompetitionBase competition,
        bool isNew)
    {
        if (dto is not EspnBaseballEventCompetitionDto baseballDto
            || competition is not BaseballCompetition baseballCompetition)
        {
            return;
        }

        if (baseballDto.Series is null || baseballDto.Series.Count == 0)
        {
            return;
        }

        if (!command.SeasonYear.HasValue)
        {
            _logger.LogWarning(
                "Skipping series ingestion: command missing SeasonYear. CompetitionId={CompId}",
                baseballCompetition.Id);
            return;
        }

        foreach (var entry in baseballDto.Series)
        {
            switch (entry.Type)
            {
                case "current":
                    await ProcessCurrentSeries(command, entry, baseballDto, baseballCompetition);
                    break;
                case "season":
                    await ProcessSeasonSeries(command, entry, baseballCompetition, command.SeasonYear.Value);
                    break;
                case null:
                    _logger.LogWarning(
                        "Skipping series entry with null Type. CompetitionId={CompId}",
                        baseballCompetition.Id);
                    break;
                default:
                    _logger.LogInformation(
                        "Skipping series entry with unrecognized Type={Type}. CompetitionId={CompId}",
                        entry.Type, baseballCompetition.Id);
                    break;
            }
        }
    }

    private async Task ProcessCurrentSeries(
        ProcessDocumentCommand command,
        EspnBaseballSeriesDto entry,
        EspnBaseballEventCompetitionDto baseballDto,
        BaseballCompetition competition)
    {
        if (string.IsNullOrWhiteSpace(baseballDto.SeriesId))
        {
            _logger.LogWarning(
                "Current series entry has no parent SeriesId. Skipping. CompetitionId={CompId}",
                competition.Id);
            return;
        }

        var seriesId = DeterministicGuid.Combine("series", baseballDto.SeriesId);

        var existing = await _dataContext.Series
            .Include(x => x.Competitors)
            .FirstOrDefaultAsync(x => x.Id == seriesId);

        if (existing is null)
        {
            existing = new Series
            {
                Id = seriesId,
                EspnSeriesId = baseballDto.SeriesId,
                CreatedBy = command.CorrelationId
            };
            await _dataContext.Series.AddAsync(existing);
        }
        else
        {
            existing.ModifiedUtc = DateTime.UtcNow;
            existing.ModifiedBy = command.CorrelationId;
        }

        existing.Title = entry.Title;
        existing.Description = entry.Description;
        existing.Summary = entry.Summary;
        existing.Completed = entry.Completed;
        existing.TotalCompetitions = entry.TotalCompetitions;
        existing.StartDate = ParseStartDate(entry.StartDate);

        await UpsertSeriesCompetitors(command, existing, seriesId, entry.Competitors);

        competition.CurrentSeriesId = seriesId;
    }

    private async Task ProcessSeasonSeries(
        ProcessDocumentCommand command,
        EspnBaseballSeriesDto entry,
        BaseballCompetition competition,
        int seasonYear)
    {
        if (entry.Competitors is null || entry.Competitors.Count != 2)
        {
            _logger.LogWarning(
                "Season series requires exactly 2 competitors; got {Count}. Skipping. CompetitionId={CompId}",
                entry.Competitors?.Count ?? 0,
                competition.Id);
            return;
        }

        // Resolve both team refs to FranchiseSeasonIds. If either fails to
        // resolve, skip — the season-series row depends on both being in
        // hand to canonicalize the (low, high) pair identity.
        var resolved = new List<(Guid FranchiseSeasonId, EspnBaseballSeriesCompetitorDto Dto)>();
        foreach (var c in entry.Competitors)
        {
            if (c.Team?.Ref is null)
            {
                _logger.LogWarning(
                    "Season series competitor missing team ref. Skipping. CompetitionId={CompId}",
                    competition.Id);
                return;
            }

            var fsId = await _dataContext.ResolveIdAsync<FranchiseSeason, FranchiseSeasonExternalId>(
                c.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons,
                externalIdsNav: "ExternalIds",
                key: fs => fs.Id);

            if (fsId is null)
            {
                _logger.LogWarning(
                    "Could not resolve FranchiseSeason for season-series competitor. TeamRef={Ref} CompetitionId={CompId}. Skipping.",
                    c.Team.Ref, competition.Id);
                return;
            }

            resolved.Add((fsId.Value, c));
        }

        // Sort by Guid value so the pair identity is canonical regardless of
        // which team is listed first in any given competition's payload.
        var sorted = resolved.OrderBy(x => x.FranchiseSeasonId).ToList();
        var lowId = sorted[0].FranchiseSeasonId;
        var highId = sorted[1].FranchiseSeasonId;

        var seasonSeriesId = DeterministicGuid.Combine(
            "season-series",
            seasonYear.ToString(CultureInfo.InvariantCulture),
            lowId.ToString(),
            highId.ToString());

        var existing = await _dataContext.SeasonSeries
            .Include(x => x.Competitors)
            .FirstOrDefaultAsync(x => x.Id == seasonSeriesId);

        if (existing is null)
        {
            existing = new SeasonSeries
            {
                Id = seasonSeriesId,
                SeasonYear = seasonYear,
                FranchiseSeasonALowId = lowId,
                FranchiseSeasonBHighId = highId,
                CreatedBy = command.CorrelationId
            };
            await _dataContext.SeasonSeries.AddAsync(existing);
        }
        else
        {
            existing.ModifiedUtc = DateTime.UtcNow;
            existing.ModifiedBy = command.CorrelationId;
        }

        existing.Title = entry.Title;
        existing.Description = entry.Description;
        existing.Summary = entry.Summary;
        existing.Completed = entry.Completed;
        existing.TotalCompetitions = entry.TotalCompetitions;
        existing.StartDate = ParseStartDate(entry.StartDate);

        UpsertSeasonSeriesCompetitors(command, existing, seasonSeriesId, resolved);

        competition.SeasonSeriesId = seasonSeriesId;
    }

    private async Task UpsertSeriesCompetitors(
        ProcessDocumentCommand command,
        Series series,
        Guid seriesId,
        List<EspnBaseballSeriesCompetitorDto>? competitors)
    {
        if (competitors is null) return;

        foreach (var c in competitors)
        {
            if (c.Team?.Ref is null) continue;

            var fsId = await _dataContext.ResolveIdAsync<FranchiseSeason, FranchiseSeasonExternalId>(
                c.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons,
                externalIdsNav: "ExternalIds",
                key: fs => fs.Id);

            if (fsId is null)
            {
                _logger.LogWarning(
                    "Could not resolve FranchiseSeason for series competitor. TeamRef={Ref} SeriesId={SeriesId}. Skipping competitor row.",
                    c.Team.Ref, seriesId);
                continue;
            }

            var competitor = series.Competitors.FirstOrDefault(x => x.FranchiseSeasonId == fsId.Value);
            if (competitor is null)
            {
                competitor = new SeriesCompetitor
                {
                    Id = DeterministicGuid.Combine("series-competitor", seriesId.ToString(), fsId.Value.ToString()),
                    SeriesId = seriesId,
                    FranchiseSeasonId = fsId.Value,
                    CreatedBy = command.CorrelationId
                };
                series.Competitors.Add(competitor);
            }
            else
            {
                competitor.ModifiedUtc = DateTime.UtcNow;
                competitor.ModifiedBy = command.CorrelationId;
            }

            competitor.Wins = c.Wins;
            competitor.Ties = c.Ties;
        }
    }

    private void UpsertSeasonSeriesCompetitors(
        ProcessDocumentCommand command,
        SeasonSeries seasonSeries,
        Guid seasonSeriesId,
        List<(Guid FranchiseSeasonId, EspnBaseballSeriesCompetitorDto Dto)> resolved)
    {
        foreach (var (fsId, dto) in resolved)
        {
            var competitor = seasonSeries.Competitors.FirstOrDefault(x => x.FranchiseSeasonId == fsId);
            if (competitor is null)
            {
                competitor = new SeasonSeriesCompetitor
                {
                    Id = DeterministicGuid.Combine("season-series-competitor", seasonSeriesId.ToString(), fsId.ToString()),
                    SeasonSeriesId = seasonSeriesId,
                    FranchiseSeasonId = fsId,
                    CreatedBy = command.CorrelationId
                };
                seasonSeries.Competitors.Add(competitor);
            }
            else
            {
                competitor.ModifiedUtc = DateTime.UtcNow;
                competitor.ModifiedBy = command.CorrelationId;
            }

            competitor.Wins = dto.Wins;
            competitor.Ties = dto.Ties;
        }
    }

    private static DateTimeOffset? ParseStartDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }
}
