using FluentValidation;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;

public interface ICreateBaseballMlbLeagueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        CreateBaseballMlbLeagueRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

public class CreateBaseballMlbLeagueCommandHandler
    : CreateLeagueCommandHandlerBase<CreateBaseballMlbLeagueRequest>,
      ICreateBaseballMlbLeagueCommandHandler
{
    public CreateBaseballMlbLeagueCommandHandler(
        ILogger<CreateBaseballMlbLeagueCommandHandler> logger,
        AppDataContext dbContext,
        IEventBus eventBus,
        IFranchiseClientFactory franchiseClientFactory,
        IValidator<CreateBaseballMlbLeagueRequest> validator)
        : base(logger, dbContext, eventBus, franchiseClientFactory, validator)
    {
    }

    protected override Sport SportMode => Sport.BaseballMlb;
    protected override League LeagueMode => League.MLB;

    protected override IReadOnlyList<string> GetGroupingSlugs(CreateBaseballMlbLeagueRequest request) =>
        request.DivisionSlugs;

    protected override string SlugRequestFieldName => nameof(CreateBaseballMlbLeagueRequest.DivisionSlugs);
    protected override string SlugDisplayLabel => "division";
}
