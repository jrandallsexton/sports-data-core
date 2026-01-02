using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutDrives;

public interface IGetCompetitionsWithoutDrivesQueryHandler
{
    Task<Result<List<CompetitionWithoutDrivesDto>>> ExecuteAsync(
        GetCompetitionsWithoutDrivesQuery query,
        CancellationToken cancellationToken);
}

public class GetCompetitionsWithoutDrivesQueryHandler : IGetCompetitionsWithoutDrivesQueryHandler
{
    private readonly IProvideCanonicalAdminData _canonicalAdminData;
    private readonly ILogger<GetCompetitionsWithoutDrivesQueryHandler> _logger;

    public GetCompetitionsWithoutDrivesQueryHandler(
        IProvideCanonicalAdminData canonicalAdminData,
        ILogger<GetCompetitionsWithoutDrivesQueryHandler> logger)
    {
        _canonicalAdminData = canonicalAdminData;
        _logger = logger;
    }

    public async Task<Result<List<CompetitionWithoutDrivesDto>>> ExecuteAsync(
        GetCompetitionsWithoutDrivesQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _canonicalAdminData.GetCompetitionsWithoutDrives();
            return new Success<List<CompetitionWithoutDrivesDto>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get competitions without drives");
            return new Failure<List<CompetitionWithoutDrivesDto>>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("competitions", $"Error retrieving competitions without drives: {ex.Message}")]);
        }
    }
}
