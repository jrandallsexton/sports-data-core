using SportsData.Api.Application.UI.Conferences.Dtos;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

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
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public GetConferenceNamesAndSlugsQueryHandler(
        ILogger<GetConferenceNamesAndSlugsQueryHandler> logger,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<List<ConferenceNameAndSlugDto>>> ExecuteAsync(
        GetConferenceNamesAndSlugsQuery query,
        CancellationToken cancellationToken = default)
    {
        var seasonYear = query.SeasonYear ?? DateTime.UtcNow.Year;

        _logger.LogDebug("Getting conference names and slugs for season year {SeasonYear}", seasonYear);

        var conferencesAndSlugs = await _canonicalDataProvider.GetConferenceNamesAndSlugsForSeasonYear(seasonYear);

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
