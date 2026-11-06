using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Infrastructure.Data.Common;

using System.Text.RegularExpressions;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestConferenceMembershipSourcing;

public interface IRequestConferenceMembershipSourcingCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        RequestConferenceMembershipSourcingCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Re-sources a season's group tree so every leaf group's member list
/// corrects FranchiseSeason.GroupSeasonId (and GroupSeasonMap). Publishes the
/// season's root groups with an explicit [GroupSeason, TeamSeason] filter:
/// GroupSeasonDocumentProcessor only walks children and member lists of an
/// EXISTING group when the command names those types, and a non-empty filter
/// also keeps Provider's historical republish cooldown from dropping the
/// group requests. Each member list then fans out document-only.
///
/// Seeds the season-type variant the season's team-seasons already use:
/// GroupSeason stores each group once per types/1-4, and conference W/L
/// needs one variant per season.
/// </summary>
public partial class RequestConferenceMembershipSourcingCommandHandler : IRequestConferenceMembershipSourcingCommandHandler
{
    public static readonly IReadOnlyCollection<DocumentType> LinkedDocumentTypes =
        [DocumentType.GroupSeason, DocumentType.TeamSeason];

    private readonly ILogger<RequestConferenceMembershipSourcingCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IEventBus _eventBus;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly IValidator<RequestConferenceMembershipSourcingCommand> _validator;
    private readonly IMessageDeliveryScope _deliveryScope;

    public RequestConferenceMembershipSourcingCommandHandler(
        ILogger<RequestConferenceMembershipSourcingCommandHandler> logger,
        TeamSportDataContext dataContext,
        IEventBus eventBus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IValidator<RequestConferenceMembershipSourcingCommand> validator,
        IMessageDeliveryScope deliveryScope)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventBus = eventBus;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _validator = validator;
        _deliveryScope = deliveryScope;
    }

    [GeneratedRegex(@"/types/(\d+)/groups/", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonTypePattern();

    public async Task<Result<Guid>> ExecuteAsync(
        RequestConferenceMembershipSourcingCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);

        var seasonType = await GetSeasonTypeInUseAsync(command.SeasonYear, cancellationToken);
        if (seasonType is null)
        {
            _logger.LogWarning(
                "No grouped franchise seasons to re-source. SeasonYear={SeasonYear}, Sport={Sport}",
                command.SeasonYear, command.Sport);
            // Failure, not Success: ToActionResult maps a successful NotFound to 200.
            return new Failure<Guid>(default!, ResultStatus.NotFound, []);
        }

        var rootUrls = await _dataContext.GroupSeasons
            .AsNoTracking()
            .Where(g => g.SeasonYear == command.SeasonYear && g.ParentId == null)
            .SelectMany(g => g.ExternalIds)
            .Where(x => x.Provider == SourceDataProvider.Espn)
            .Select(x => x.SourceUrl)
            .ToListAsync(cancellationToken);

        var seeds = rootUrls
            .Where(url => SeasonTypePattern().Match(url) is { Success: true } m && m.Groups[1].Value == seasonType)
            .ToList();

        // Direct delivery, NOT the bus-outbox: this handler only reads, so
        // SaveChangesAsync would never flush the outbox. Same as
        // RequestFranchiseSeasonSourcingCommandHandler.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            foreach (var url in seeds)
            {
                var identity = _externalRefIdentityGenerator.Generate(new Uri(url));

                await _eventBus.Publish(new DocumentRequested(
                    Id: identity.UrlHash,
                    ParentId: null,
                    Uri: new Uri(identity.CleanUrl),
                    Ref: null,
                    Sport: command.Sport,
                    SeasonYear: command.SeasonYear,
                    DocumentType: DocumentType.GroupSeason,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.FranchiseSeasonService,
                    IncludeLinkedDocumentTypes: LinkedDocumentTypes.ToList()
                ), cancellationToken);
            }
        }

        _logger.LogInformation(
            "Conference membership re-sourcing requested. SeasonYear={SeasonYear}, Sport={Sport}, SeasonType={SeasonType}, RootGroups={RootGroups}, CorrelationId={CorrelationId}",
            command.SeasonYear, command.Sport, seasonType, seeds.Count, command.CorrelationId);

        return new Success<Guid>(command.CorrelationId, ResultStatus.Accepted);
    }

    /// <summary>
    /// The types/{n} variant most of the season's team-seasons point at
    /// (types/3 for 2000-2024 and types/2 for 2025 in prod on 2026-11-06).
    /// </summary>
    private async Task<string?> GetSeasonTypeInUseAsync(int seasonYear, CancellationToken cancellationToken)
    {
        var urls = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Where(fs => fs.SeasonYear == seasonYear && fs.GroupSeasonId != null)
            .SelectMany(fs => fs.GroupSeason!.ExternalIds)
            .Select(x => x.SourceUrl)
            .ToListAsync(cancellationToken);

        return urls
            .Select(url => SeasonTypePattern().Match(url))
            .Where(m => m.Success)
            .GroupBy(m => m.Groups[1].Value)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
    }
}
