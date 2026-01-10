using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionDrives;

public interface IRefreshCompetitionDrivesCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(RefreshCompetitionDrivesCommand command, CancellationToken cancellationToken = default);
}

public class RefreshCompetitionDrivesCommandHandler : IRefreshCompetitionDrivesCommandHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly IEventBus _eventBus;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public RefreshCompetitionDrivesCommandHandler(
        TeamSportDataContext dataContext,
        IEventBus eventBus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _dataContext = dataContext;
        _eventBus = eventBus;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        RefreshCompetitionDrivesCommand command,
        CancellationToken cancellationToken = default)
    {
        var competition = await _dataContext.Competitions
            .Include(x => x.Contest)
            .Include(x => x.ExternalIds.Where(y => y.Provider == SourceDataProvider.Espn))
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == command.CompetitionId, cancellationToken);

        if (competition is null)
        {
            return new Failure<Guid>(
                command.CompetitionId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.CompetitionId), "Competition Not Found")]
            );
        }

        var competitionExternalId = competition.ExternalIds
            .FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);

        if (competitionExternalId is null)
        {
            return new Failure<Guid>(
                command.CompetitionId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.CompetitionId), "Competition ESPN ExternalId Not Found")]
            );
        }

        if (string.IsNullOrWhiteSpace(competitionExternalId.SourceUrl))
        {
            return new Failure<Guid>(
                command.CompetitionId,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(competitionExternalId.SourceUrl), "Competition SourceUrl is null or empty")]
            );
        }

        if (!Uri.TryCreate(competitionExternalId.SourceUrl, UriKind.Absolute, out var sourceUrl))
        {
            return new Failure<Guid>(
                command.CompetitionId,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(competitionExternalId.SourceUrl), $"Competition SourceUrl is not a valid absolute URI: {competitionExternalId.SourceUrl}")]
            );
        }

        Uri drivesRef;
        try
        {
            drivesRef = EspnUriMapper.CompetitionRefToCompetitionDrivesRef(sourceUrl);
        }
        catch (InvalidOperationException ex)
        {
            return new Failure<Guid>(
                command.CompetitionId,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(competitionExternalId.SourceUrl), $"ESPN URI mapping failed: {ex.Message}. SourceUrl: {competitionExternalId.SourceUrl}")]
            );
        }
        catch (Exception ex)
        {
            return new Failure<Guid>(
                command.CompetitionId,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(competitionExternalId.SourceUrl), $"Unexpected error mapping ESPN URI: {ex.Message}. SourceUrl: {competitionExternalId.SourceUrl}")]
            );
        }

        var drivesIdentity = _externalRefIdentityGenerator.Generate(drivesRef);

        // request sourcing?
        await _eventBus.Publish(new DocumentRequested(
            Id: drivesIdentity.UrlHash,
            ParentId: command.CompetitionId.ToString(),
            Uri: new Uri(drivesIdentity.CleanUrl),
            Ref: null,
            Sport: competition.Contest.Sport,
            SeasonYear: competition.Contest.SeasonYear,
            DocumentType: DocumentType.EventCompetitionDrive,
            SourceDataProvider: SourceDataProvider.Espn, // TODO: remove hard-coding
            CorrelationId: Guid.NewGuid(),
            CausationId: CausationId.Producer.CompetitionService
        ), cancellationToken);

        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<Guid>(command.CompetitionId, ResultStatus.Accepted);
    }
}
