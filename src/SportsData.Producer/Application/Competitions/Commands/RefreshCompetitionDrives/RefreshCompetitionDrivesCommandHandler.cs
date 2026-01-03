using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Processing;
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
            .Include(x => x.ExternalIds.Where(y => y.CompetitionId == command.CompetitionId))
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
            .FirstOrDefault();

        if (competitionExternalId is null)
        {
            return new Failure<Guid>(
                command.CompetitionId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.CompetitionId), "Competition ExternalId Not Found")]
            );
        }

        var sourceUrl = new Uri(competitionExternalId.SourceUrl);

        var drivesRef = EspnUriMapper.CompetitionRefToCompetitionDrivesRef(sourceUrl);
        var drivesIdentity = _externalRefIdentityGenerator.Generate(drivesRef);

        // request sourcing?
        await _eventBus.Publish(new DocumentRequested(
            Id: drivesIdentity.UrlHash,
            ParentId: command.CompetitionId.ToString(),
            Uri: new Uri(drivesIdentity.CleanUrl),
            Sport: Sport.FootballNcaa, // TODO: remove hard-coding
            SeasonYear: 2025, // TODO: remove hard-coding
            DocumentType: DocumentType.EventCompetitionDrive,
            SourceDataProvider: SourceDataProvider.Espn, // TODO: remove hard-coding
            CorrelationId: Guid.NewGuid(),
            CausationId: CausationId.Producer.CompetitionService
        ));

        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<Guid>(command.CompetitionId, ResultStatus.Accepted);
    }
}
