using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.TeamCard.Queries.GetTeamSchedule;

public interface IGetTeamScheduleQueryHandler
{
    Task<Result<List<TeamCardScheduleItemDto>>> ExecuteAsync(
        GetTeamScheduleQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamScheduleQueryHandler : IGetTeamScheduleQueryHandler
{
    private readonly ILogger<GetTeamScheduleQueryHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public GetTeamScheduleQueryHandler(
        ILogger<GetTeamScheduleQueryHandler> logger,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<List<TeamCardScheduleItemDto>>> ExecuteAsync(
        GetTeamScheduleQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var mode = ModeMapper.ResolveMode(query.Sport, query.League);
            var client = _franchiseClientFactory.Resolve(mode);
            return await client.GetTeamSchedule(query.Slug, query.SeasonYear, query.AsOfDate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving team schedule for sport={Sport}, league={League}, slug={Slug}, seasonYear={SeasonYear}, asOfDate={AsOfDate}",
                query.Sport, query.League, query.Slug, query.SeasonYear, query.AsOfDate);

            return new Failure<List<TeamCardScheduleItemDto>>(
                new List<TeamCardScheduleItemDto>(),
                ResultStatus.Error,
                [new ValidationFailure("TeamSchedule", "Error retrieving team schedule. Please try again later.")]);
        }
    }
}
