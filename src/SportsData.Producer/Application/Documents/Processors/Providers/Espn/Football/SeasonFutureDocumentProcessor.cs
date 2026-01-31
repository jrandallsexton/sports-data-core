using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonFuture)]
public class SeasonFutureDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public SeasonFutureDocumentProcessor(
        ILogger<SeasonFutureDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
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
        var dto = command.Document.FromJson<EspnFootballSeasonFutureDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFootballSeasonFutureDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnFootballSeasonFutureDto Ref is null or empty. {@Command}", command);
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("SeasonYear is required for {DocumentType}", command.DocumentType);
            throw new InvalidOperationException($"SeasonYear was not provided. CorrelationId: {command.CorrelationId}");
        }

        // Get canonical Season
        var season = await _dataContext.Seasons
            .FirstOrDefaultAsync(s => s.Year == command.Season.Value);

        if (season is null)
        {
            _logger.LogError("Season not found for year {SeasonYear}", command.Season.Value);
            return;
        }

        // Check for existing
        var exists = await _dataContext.SeasonFutures
            .AnyAsync(x => x.ExternalIds.Any(z => z.Value == command.UrlHash &&
                                                  z.Provider == command.SourceDataProvider));

        if (exists)
        {
            // TODO: Potentially update existing record if needed
            _logger.LogInformation("SeasonFuture already exists for UrlHash {UrlHash}", command.UrlHash);
            return;
        }

        // Initial mapping from DTO ? Entity (without Books yet)
        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            season.Id,
            command.CorrelationId,
            command.UrlHash,
            command.SourceDataProvider
        );

        // Populate Books by resolving FranchiseSeasonId per Book
        foreach (var itemDto in dto.Futures ?? Enumerable.Empty<EspnFootballSeasonFutureItemDto>())
        {
            var item = entity.Items.FirstOrDefault(i =>
                i.ProviderId == itemDto.Provider.Id &&
                i.ProviderName == itemDto.Provider.Name);

            if (item == null)
            {
                _logger.LogWarning("Provider mismatch in SeasonFuture mapping: {ProviderId}, {ProviderName}",
                    itemDto.Provider.Id, itemDto.Provider.Name);
                continue;
            }

            foreach (var bookDto in itemDto.Books ?? Enumerable.Empty<EspnFootballSeasonFutureBookDto>())
            {
                if (bookDto.Team?.Ref == null)
                {
                    _logger.LogWarning("Book missing team ref for Provider {ProviderName}", item.ProviderName);
                    continue;
                }

                var franchiseSeasonId = await _dataContext.ResolveIdAsync<
                    FranchiseSeason, FranchiseSeasonExternalId>(
                    bookDto.Team,
                    command.SourceDataProvider,
                    () => _dataContext.FranchiseSeasons.Where(fs => fs.SeasonYear == season.Year),
                    externalIdsNav: "ExternalIds",
                    key: fs => fs.Id);

                if (!franchiseSeasonId.HasValue)
                {
                    _logger.LogWarning("No FranchiseSeason mapping found for Team Ref {TeamRef}", bookDto.Team?.Ref);
                    continue;
                }

                item.Books.Add(new SeasonFutureBook
                {
                    Id = Guid.NewGuid(),
                    SeasonFutureItemId = item.Id,
                    FranchiseSeasonId = franchiseSeasonId.Value,
                    Value = bookDto.Value,
                    CreatedBy = command.CorrelationId,
                    CreatedUtc = DateTime.UtcNow
                });
            }
        }

        // Save to DB
        await _dataContext.SeasonFutures.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("SeasonFuture persisted for season {SeasonId}", season.Id);
    }


}
