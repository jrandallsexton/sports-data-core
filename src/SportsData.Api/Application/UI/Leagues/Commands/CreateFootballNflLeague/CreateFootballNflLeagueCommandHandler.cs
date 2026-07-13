using FluentValidation;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague;

public interface ICreateFootballNflLeagueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        CreateFootballNflLeagueRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

public class CreateFootballNflLeagueCommandHandler
    : CreateLeagueCommandHandlerBase<CreateFootballNflLeagueRequest>,
      ICreateFootballNflLeagueCommandHandler
{
    public CreateFootballNflLeagueCommandHandler(
        ILogger<CreateFootballNflLeagueCommandHandler> logger,
        AppDataContext dbContext,
        IEventBus eventBus,
        IFranchiseClientFactory franchiseClientFactory,
        IContestClientFactory contestClientFactory,
        IValidator<CreateFootballNflLeagueRequest> validator,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dbContext, eventBus, franchiseClientFactory, contestClientFactory, validator, dateTimeProvider)
    {
    }

    protected override Sport SportMode => Sport.FootballNfl;
    protected override League LeagueMode => League.NFL;

    protected override IReadOnlyList<string> GetGroupingSlugs(CreateFootballNflLeagueRequest request) =>
        request.DivisionSlugs;

    protected override string SlugRequestFieldName => nameof(CreateFootballNflLeagueRequest.DivisionSlugs);
    protected override string SlugDisplayLabel => "division";
}
