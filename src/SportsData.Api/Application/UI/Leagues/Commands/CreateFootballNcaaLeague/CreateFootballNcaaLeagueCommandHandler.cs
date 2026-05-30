using FluentValidation;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague;

public interface ICreateFootballNcaaLeagueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        CreateFootballNcaaLeagueRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

public class CreateFootballNcaaLeagueCommandHandler
    : CreateLeagueCommandHandlerBase<CreateFootballNcaaLeagueRequest>,
      ICreateFootballNcaaLeagueCommandHandler
{
    public CreateFootballNcaaLeagueCommandHandler(
        ILogger<CreateFootballNcaaLeagueCommandHandler> logger,
        AppDataContext dbContext,
        IEventBus eventBus,
        IFranchiseClientFactory franchiseClientFactory,
        IValidator<CreateFootballNcaaLeagueRequest> validator,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dbContext, eventBus, franchiseClientFactory, validator, dateTimeProvider)
    {
    }

    protected override Sport SportMode => Sport.FootballNcaa;
    protected override League LeagueMode => League.NCAAF;

    protected override IReadOnlyList<string> GetGroupingSlugs(CreateFootballNcaaLeagueRequest request) =>
        request.ConferenceSlugs;

    protected override string SlugRequestFieldName => nameof(CreateFootballNcaaLeagueRequest.ConferenceSlugs);
    protected override string SlugDisplayLabel => "conference";

    protected override void ApplySportSpecific(PickemGroup group, CreateFootballNcaaLeagueRequest request)
    {
        group.RankingFilter = string.IsNullOrWhiteSpace(request.RankingFilter)
            ? null
            : Enum.Parse<TeamRankingFilter>(request.RankingFilter, ignoreCase: true);
    }
}
