using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutPlays;

public interface IGetCompetitionsWithoutPlaysQueryHandler
{
    Task<Result<List<CompetitionWithoutPlaysDto>>> ExecuteAsync(
        GetCompetitionsWithoutPlaysQuery query,
        CancellationToken cancellationToken);
}

public class GetCompetitionsWithoutPlaysQueryHandler : IGetCompetitionsWithoutPlaysQueryHandler
{
    private readonly IProvideCanonicalAdminData _canonicalAdminData;
    private readonly ILogger<GetCompetitionsWithoutPlaysQueryHandler> _logger;

    public GetCompetitionsWithoutPlaysQueryHandler(
        IProvideCanonicalAdminData canonicalAdminData,
        ILogger<GetCompetitionsWithoutPlaysQueryHandler> logger)
    {
        _canonicalAdminData = canonicalAdminData;
        _logger = logger;
    }

    public async Task<Result<List<CompetitionWithoutPlaysDto>>> ExecuteAsync(
        GetCompetitionsWithoutPlaysQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _canonicalAdminData.GetCompetitionsWithoutPlays(cancellationToken);
            return new Success<List<CompetitionWithoutPlaysDto>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get competitions without plays");
            return new Failure<List<CompetitionWithoutPlaysDto>>(
                new List<CompetitionWithoutPlaysDto>(),
                ResultStatus.Error,
                [new ValidationFailure("competitions", "An error occurred while retrieving competitions without plays")]);
        }
    }
}
