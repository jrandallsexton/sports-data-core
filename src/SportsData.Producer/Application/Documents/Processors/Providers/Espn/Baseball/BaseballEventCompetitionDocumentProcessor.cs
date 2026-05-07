using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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

    // Inline series snapshot. ESPN ships current-series and season-series
    // state inline on the competition payload. We snapshot the relevant
    // fields onto BaseballCompetition itself, locking them on first
    // non-null write so historical matchup pages render at-game-start
    // state instead of current rolled-up state. EspnSeriesId is the
    // grouping key (not historical state) and refreshes every pass.
    //
    // See docs/series-snapshot-redesign.md.
    protected override Task ProcessSportSpecificCompetitionData(
        ProcessDocumentCommand command,
        EspnEventCompetitionDtoBase dto,
        CompetitionBase competition,
        bool isNew)
    {
        if (dto is not EspnBaseballEventCompetitionDto baseballDto
            || competition is not BaseballCompetition baseballCompetition)
        {
            return Task.CompletedTask;
        }

        // Restore previously-locked snapshot columns. The base class's
        // SetValues(updatedEntity) blanks our snapshot columns on every
        // reprocess (AsBaseballEntity doesn't carry them), which would
        // defeat lock-on-first-write. Reading from the change tracker's
        // OriginalValue recovers the DB-side values from before SetValues.
        RestoreSnapshotFromOriginalValues(baseballCompetition);

        if (!string.IsNullOrWhiteSpace(baseballDto.SeriesId))
        {
            baseballCompetition.EspnSeriesId = baseballDto.SeriesId;
        }

        if (baseballDto.Series is null || baseballDto.Series.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var entry in baseballDto.Series)
        {
            switch (entry.Type)
            {
                case "current":
                    ApplyCurrentSeriesSnapshot(baseballDto, entry, baseballCompetition);
                    break;
                case "season":
                    ApplySeasonSeriesSnapshot(baseballDto, entry, baseballCompetition);
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

        return Task.CompletedTask;
    }

    private void ApplyCurrentSeriesSnapshot(
        EspnBaseballEventCompetitionDto baseballDto,
        EspnBaseballSeriesDto entry,
        BaseballCompetition competition)
    {
        if (competition.CurrentSeriesSummary is not null)
        {
            return;
        }

        if (!TryMapHomeAway(baseballDto, entry, competition.Id, out var home, out var away))
        {
            return;
        }

        competition.CurrentSeriesSummary = entry.Summary;
        competition.CurrentSeriesTotalCompetitions = entry.TotalCompetitions;
        competition.CurrentSeriesCompleted = entry.Completed;
        competition.CurrentSeriesStartDate = ParseStartDate(entry.StartDate);
        competition.CurrentSeriesHomeWins = home.Wins;
        competition.CurrentSeriesHomeTies = home.Ties;
        competition.CurrentSeriesAwayWins = away.Wins;
        competition.CurrentSeriesAwayTies = away.Ties;
    }

    private void ApplySeasonSeriesSnapshot(
        EspnBaseballEventCompetitionDto baseballDto,
        EspnBaseballSeriesDto entry,
        BaseballCompetition competition)
    {
        if (competition.SeasonSeriesSummary is not null)
        {
            return;
        }

        if (!TryMapHomeAway(baseballDto, entry, competition.Id, out var home, out var away))
        {
            return;
        }

        competition.SeasonSeriesSummary = entry.Summary;
        competition.SeasonSeriesTotalCompetitions = entry.TotalCompetitions;
        competition.SeasonSeriesCompleted = entry.Completed;
        competition.SeasonSeriesHomeWins = home.Wins;
        competition.SeasonSeriesHomeTies = home.Ties;
        competition.SeasonSeriesAwayWins = away.Wins;
        competition.SeasonSeriesAwayTies = away.Ties;
    }

    private bool TryMapHomeAway(
        EspnBaseballEventCompetitionDto baseballDto,
        EspnBaseballSeriesDto entry,
        Guid competitionId,
        out EspnBaseballSeriesCompetitorDto home,
        out EspnBaseballSeriesCompetitorDto away)
    {
        home = null!;
        away = null!;

        if (entry.Competitors is null || entry.Competitors.Count != 2)
        {
            _logger.LogWarning(
                "Series entry needs exactly 2 competitors; got {Count}. Skipping. CompetitionId={CompId}",
                entry.Competitors?.Count ?? 0,
                competitionId);
            return false;
        }

        if (baseballDto.Competitors is null)
        {
            _logger.LogWarning(
                "EventCompetition payload has no parent competitors collection. Cannot map home/away for series. CompetitionId={CompId}",
                competitionId);
            return false;
        }

        var homeId = baseballDto.Competitors.FirstOrDefault(c => c.HomeAway == "home")?.Id;
        var awayId = baseballDto.Competitors.FirstOrDefault(c => c.HomeAway == "away")?.Id;

        if (string.IsNullOrEmpty(homeId) || string.IsNullOrEmpty(awayId))
        {
            _logger.LogWarning(
                "EventCompetition payload missing home or away competitor. Cannot map series. CompetitionId={CompId}",
                competitionId);
            return false;
        }

        var matchedHome = entry.Competitors.FirstOrDefault(c => c.Id == homeId);
        var matchedAway = entry.Competitors.FirstOrDefault(c => c.Id == awayId);

        if (matchedHome is null || matchedAway is null)
        {
            _logger.LogWarning(
                "Series competitor ids did not match parent home/away ids. CompetitionId={CompId} HomeId={HomeId} AwayId={AwayId}",
                competitionId, homeId, awayId);
            return false;
        }

        home = matchedHome;
        away = matchedAway;
        return true;
    }

    private void RestoreSnapshotFromOriginalValues(BaseballCompetition c)
    {
        var entry = _dataContext.Entry(c);
        if (entry.State == EntityState.Added || entry.State == EntityState.Detached)
        {
            return;
        }

        c.CurrentSeriesSummary = Original<string?>(entry, nameof(BaseballCompetition.CurrentSeriesSummary));
        c.CurrentSeriesTotalCompetitions = Original<int?>(entry, nameof(BaseballCompetition.CurrentSeriesTotalCompetitions));
        c.CurrentSeriesCompleted = Original<bool?>(entry, nameof(BaseballCompetition.CurrentSeriesCompleted));
        c.CurrentSeriesStartDate = Original<DateTimeOffset?>(entry, nameof(BaseballCompetition.CurrentSeriesStartDate));
        c.CurrentSeriesHomeWins = Original<int?>(entry, nameof(BaseballCompetition.CurrentSeriesHomeWins));
        c.CurrentSeriesHomeTies = Original<int?>(entry, nameof(BaseballCompetition.CurrentSeriesHomeTies));
        c.CurrentSeriesAwayWins = Original<int?>(entry, nameof(BaseballCompetition.CurrentSeriesAwayWins));
        c.CurrentSeriesAwayTies = Original<int?>(entry, nameof(BaseballCompetition.CurrentSeriesAwayTies));

        c.SeasonSeriesSummary = Original<string?>(entry, nameof(BaseballCompetition.SeasonSeriesSummary));
        c.SeasonSeriesTotalCompetitions = Original<int?>(entry, nameof(BaseballCompetition.SeasonSeriesTotalCompetitions));
        c.SeasonSeriesCompleted = Original<bool?>(entry, nameof(BaseballCompetition.SeasonSeriesCompleted));
        c.SeasonSeriesHomeWins = Original<int?>(entry, nameof(BaseballCompetition.SeasonSeriesHomeWins));
        c.SeasonSeriesHomeTies = Original<int?>(entry, nameof(BaseballCompetition.SeasonSeriesHomeTies));
        c.SeasonSeriesAwayWins = Original<int?>(entry, nameof(BaseballCompetition.SeasonSeriesAwayWins));
        c.SeasonSeriesAwayTies = Original<int?>(entry, nameof(BaseballCompetition.SeasonSeriesAwayTies));
    }

    private static T Original<T>(EntityEntry entry, string propertyName)
        => (T)entry.Property(propertyName).OriginalValue!;

    private static DateTimeOffset? ParseStartDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }
}
