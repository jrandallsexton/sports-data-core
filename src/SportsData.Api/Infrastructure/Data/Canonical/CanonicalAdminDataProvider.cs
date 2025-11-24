using Dapper;

using SportsData.Api.Infrastructure.Data.Canonical.Models;

using System.Data;

namespace SportsData.Api.Infrastructure.Data.Canonical;

public interface IProvideCanonicalAdminData
{
    Task<List<CompetitionWithoutCompetitorsDto>> GetCompetitionsWithoutCompetitors();
    Task<List<CompetitionWithoutPlaysDto>> GetCompetitionsWithoutPlays();
    Task<List<CompetitionWithoutDrivesDto>> GetCompetitionsWithoutDrives();
    Task<List<CompetitionWithoutMetricsDto>> GetCompetitionsWithoutMetrics();
}

public class CanonicalAdminDataProvider : IProvideCanonicalAdminData
{
    private readonly IDbConnection _connection;
    private readonly ILogger<CanonicalAdminDataProvider> _logger;
    private readonly CanonicalAdminDataQueryProvider _queryProvider;

    public CanonicalAdminDataProvider(
        ILogger<CanonicalAdminDataProvider> logger,
        IDbConnection connection,
        CanonicalAdminDataQueryProvider queryProvider)
    {
        _logger = logger;
        _connection = connection;
        _queryProvider = queryProvider;
    }

    public async Task<List<CompetitionWithoutCompetitorsDto>> GetCompetitionsWithoutCompetitors()
    {
        var sql = _queryProvider.GetCompetitionsWithoutCompetitors();

        try
        {
            var results = await _connection.QueryAsync<CompetitionWithoutCompetitorsDto>(sql);
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch competitions without competitors");
            throw;
        }
    }

    public async Task<List<CompetitionWithoutPlaysDto>> GetCompetitionsWithoutPlays()
    {
        var sql = _queryProvider.GetCompetitionsWithoutPlays();

        try
        {
            var results = await _connection.QueryAsync<CompetitionWithoutPlaysDto>(sql);
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch competitions without plays");
            throw;
        }
    }

    public async Task<List<CompetitionWithoutDrivesDto>> GetCompetitionsWithoutDrives()
    {
        var sql = _queryProvider.GetCompetitionsWithoutDrives();

        try
        {
            var results = await _connection.QueryAsync<CompetitionWithoutDrivesDto>(sql);
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch competitions without drives");
            throw;
        }
    }

    public async Task<List<CompetitionWithoutMetricsDto>> GetCompetitionsWithoutMetrics()
    {
        var sql = _queryProvider.GetCompetitionsWithoutMetrics();

        try
        {
            var results = await _connection.QueryAsync<CompetitionWithoutMetricsDto>(sql);
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch competitions without metrics");
            throw;
        }
    }
}