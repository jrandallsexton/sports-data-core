using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common.Draft;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.DraftRounds)]
public class DraftRoundsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public DraftRoundsDocumentProcessor(
        ILogger<DraftRoundsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnDraftRoundsDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnDraftRoundsDto. {@Command}", command);
            return;
        }

        if (!command.SeasonYear.HasValue)
        {
            _logger.LogError("SeasonYear is required for {DocumentType}", command.DocumentType);
            throw new InvalidOperationException(
                $"SeasonYear was not provided. CorrelationId: {command.CorrelationId}");
        }

        var draftYear = command.SeasonYear.Value;

        // Resolve parent Draft entity by year
        var draft = await _dataContext.Drafts
            .FirstOrDefaultAsync(d => d.Year == draftYear);

        if (draft is null)
        {
            _logger.LogError(
                "Draft entity not found for year {DraftYear}. Cannot process rounds without parent Draft.",
                draftYear);
            throw new InvalidOperationException(
                $"Draft entity not found for year {draftYear}. CorrelationId: {command.CorrelationId}");
        }

        // Idempotent: remove existing rounds (and their picks via cascade) before re-inserting
        var existingRounds = await _dataContext.DraftRounds
            .Where(r => r.DraftId == draft.Id)
            .ToListAsync();

        if (existingRounds.Count > 0)
        {
            _logger.LogInformation(
                "Removing {Count} existing draft rounds for year {DraftYear} before re-processing",
                existingRounds.Count, draftYear);
            _dataContext.DraftRounds.RemoveRange(existingRounds);
            await _dataContext.SaveChangesAsync();
        }

        var roundEntities = new List<DraftRound>();

        foreach (var round in dto.Items)
        {
            var roundUri = new Uri(
                $"https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/{draftYear}/draft/rounds/{round.Number}");
            var roundId = _externalRefIdentityGenerator.Generate(roundUri).CanonicalId;

            var roundEntity = new DraftRound
            {
                Id = roundId,
                DraftId = draft.Id,
                Number = round.Number,
                DisplayName = round.DisplayName,
                ShortDisplayName = round.ShortDisplayName,
                CreatedBy = command.CorrelationId,
                CreatedUtc = DateTime.UtcNow
            };

            foreach (var pick in round.Picks)
            {
                var pickUri = new Uri(
                    $"https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/{draftYear}/draft/rounds/{round.Number}/picks/{pick.Overall}");
                var pickId = _externalRefIdentityGenerator.Generate(pickUri).CanonicalId;

                roundEntity.Picks.Add(new DraftPick
                {
                    Id = pickId,
                    DraftRoundId = roundId,
                    Pick = pick.Pick,
                    Overall = pick.Overall,
                    Traded = pick.Traded,
                    TradeNote = pick.TradeNote,
                    AthleteRef = pick.Athlete?.Ref?.ToString(),
                    TeamRef = pick.Team?.Ref?.ToString(),
                    StatusName = pick.Status?.Name,
                    CreatedBy = command.CorrelationId,
                    CreatedUtc = DateTime.UtcNow
                });
            }

            roundEntities.Add(roundEntity);
        }

        if (roundEntities.Count > 0)
        {
            await _dataContext.DraftRounds.AddRangeAsync(roundEntities);
            await _dataContext.SaveChangesAsync();

            var totalPicks = roundEntities.Sum(r => r.Picks.Count);
            _logger.LogInformation(
                "Persisted {RoundCount} draft rounds with {PickCount} total picks for year {DraftYear}",
                roundEntities.Count, totalPicks, draftYear);
        }
        else
        {
            _logger.LogWarning("No draft rounds found in document for year {DraftYear}", draftYear);
        }
    }
}
