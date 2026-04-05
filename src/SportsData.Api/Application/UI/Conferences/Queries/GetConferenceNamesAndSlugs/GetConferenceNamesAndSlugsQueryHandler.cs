using SportsData.Api.Application.UI.Conferences.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Conferences.Queries.GetConferenceNamesAndSlugs;

public interface IGetConferenceNamesAndSlugsQueryHandler
{
    Task<Result<List<ConferenceNameAndSlugDto>>> ExecuteAsync(
        GetConferenceNamesAndSlugsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetConferenceNamesAndSlugsQueryHandler : IGetConferenceNamesAndSlugsQueryHandler
{
    private readonly ILogger<GetConferenceNamesAndSlugsQueryHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public GetConferenceNamesAndSlugsQueryHandler(
        ILogger<GetConferenceNamesAndSlugsQueryHandler> logger,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<List<ConferenceNameAndSlugDto>>> ExecuteAsync(
        GetConferenceNamesAndSlugsQuery query,
        CancellationToken cancellationToken = default)
    {
        var seasonYear = query.SeasonYear ?? DateTime.UtcNow.Year;

        _logger.LogDebug("Getting conference names and slugs for season year {SeasonYear}", seasonYear);

        // TODO: multi-sport - resolve from context
        var client = _franchiseClientFactory.Resolve(Sport.FootballNcaa);
        var conferencesAndSlugs = await client.GetConferenceNamesAndSlugs(seasonYear, cancellationToken);

        var result = conferencesAndSlugs
            .Select(item => new ConferenceNameAndSlugDto
            {
                ShortName = item.ShortName,
                Slug = item.Slug,
                Division = item.Division
            })
            .ToList();

        _logger.LogInformation(
            "Found {Count} conferences for season year {SeasonYear}",
            result.Count,
            seasonYear);

        return new Success<List<ConferenceNameAndSlugDto>>(result);
    }
}
