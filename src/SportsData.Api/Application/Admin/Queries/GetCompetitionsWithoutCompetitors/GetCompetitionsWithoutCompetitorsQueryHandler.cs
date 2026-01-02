using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutCompetitors;

public interface IGetCompetitionsWithoutCompetitorsQueryHandler
{
    Task<Result<List<CompetitionWithoutCompetitorsDto>>> ExecuteAsync(
        GetCompetitionsWithoutCompetitorsQuery query, 
        CancellationToken cancellationToken);
}

public class GetCompetitionsWithoutCompetitorsQueryHandler : IGetCompetitionsWithoutCompetitorsQueryHandler
{
    private readonly IProvideCanonicalAdminData _canonicalAdminData;
    private readonly ILogger<GetCompetitionsWithoutCompetitorsQueryHandler> _logger;

    public GetCompetitionsWithoutCompetitorsQueryHandler(
        IProvideCanonicalAdminData canonicalAdminData,
        ILogger<GetCompetitionsWithoutCompetitorsQueryHandler> logger)
    {
        _canonicalAdminData = canonicalAdminData;
        _logger = logger;
    }

    public async Task<Result<List<CompetitionWithoutCompetitorsDto>>> ExecuteAsync(
        GetCompetitionsWithoutCompetitorsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _canonicalAdminData.GetCompetitionsWithoutCompetitors(cancellationToken);
            return new Success<List<CompetitionWithoutCompetitorsDto>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get competitions without competitors");
            return new Failure<List<CompetitionWithoutCompetitorsDto>>(
                new List<CompetitionWithoutCompetitorsDto>(),
                ResultStatus.Error,
                [new ValidationFailure("competitions", "An error occurred while retrieving competitions without competitors")]);
        }
    }
}
