using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionOdds)]
public class EventCompetitionOddsDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<EventCompetitionOddsDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _db;
    private readonly IEventBus _bus;
    private readonly IGenerateExternalRefIdentities _idGen;
    private readonly IJsonHashCalculator _jsonHashCalculator;

    public EventCompetitionOddsDocumentProcessor(
        ILogger<EventCompetitionOddsDocumentProcessor<TDataContext>> logger,
        TDataContext db,
        IEventBus bus,
        IGenerateExternalRefIdentities idGen,
        IJsonHashCalculator jsonHashCalculator)
    {
        _logger = logger;
        _db = db;
        _bus = bus;
        _idGen = idGen;
        _jsonHashCalculator = jsonHashCalculator;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId
               }))
        {
            _logger.LogInformation("Began with {@command}", command);

            try
            {
                await ProcessInternal(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalDto = command.Document.FromJson<EspnEventCompetitionOddsDto>();

        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnEventCompetitionDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionDto Ref is null. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(command.ParentId))
        {
            _logger.LogError("ParentId not provided. Cannot process competition for null CompetitionId");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("Invalid ParentId format for CompetitionId. Cannot parse to Guid.");
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("Command must have a SeasonYear defined");
            return;
        }

        var competition = await _db.Competitions
            .Include(x => x.Contest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionId);

        if (competition is null)
        {
            _logger.LogError("Competition not found");
            throw new ArgumentException("competition not found");
        }

        var existing = await _db.CompetitionOdds
            .AsNoTracking()
            .Include(x => x.ExternalIds)
            .Include(x => x.Teams).ThenInclude(t => t.Snapshots)
            .Include(x => x.Totals)
            .FirstOrDefaultAsync(x =>
                x.ExternalIds.Any(e => e.SourceUrlHash == command.UrlHash &&
                                       e.Provider == command.SourceDataProvider));

        var contentHash = _jsonHashCalculator.NormalizeAndHash(command.Document);

        if (existing is null)
        {
            await ProcessNew(command, externalDto, competition.Id, competition.Contest, contentHash);
        }
        else
        {
            await ProcessUpdate(command, externalDto, existing, competition.Id);
        }
    }

    private async Task ProcessNew(
        ProcessDocumentCommand command,
        EspnEventCompetitionOddsDto dto,
        Guid competitionId,
        Contest contest,
        string contentHash)
    {
        var entity = dto.AsEntity(
            externalRefIdentityGenerator: _idGen,
            competitionId: competitionId,
            homeFranchiseSeasonId: contest.HomeTeamFranchiseSeasonId,
            awayFranchiseSeasonId: contest.AwayTeamFranchiseSeasonId,
            correlationId: command.CorrelationId,
            contentHash: contentHash);

        await _db.CompetitionOdds.AddAsync(entity);
        await _db.SaveChangesAsync();

        //await _bus.Publish(new ContestOddsCreated(
        //    contest.Id, command.CorrelationId, CausationId.Producer.EventDocumentProcessor));
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventCompetitionOddsDto dto,
        CompetitionOdds existing,
        Guid competitionId)
    {
        _logger.LogError("Update was detected. Not implemented");
        await Task.Delay(100);
    }
}