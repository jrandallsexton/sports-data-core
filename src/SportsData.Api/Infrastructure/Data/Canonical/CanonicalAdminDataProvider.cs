using Dapper;

using SportsData.Api.Infrastructure.Data.Canonical.Models;

using System.Data;

namespace SportsData.Api.Infrastructure.Data.Canonical;

public interface IProvideCanonicalAdminData
{
    Task<List<CompetitionWithoutCompetitorsDto>> GetCompetitionsWithoutCompetitors(CancellationToken cancellationToken = default);
    Task<List<CompetitionWithoutPlaysDto>> GetCompetitionsWithoutPlays(CancellationToken cancellationToken = default);
    Task<List<CompetitionWithoutDrivesDto>> GetCompetitionsWithoutDrives(CancellationToken cancellationToken = default);
    Task<List<CompetitionWithoutMetricsDto>> GetCompetitionsWithoutMetrics(CancellationToken cancellationToken = default);
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

    public async Task<List<CompetitionWithoutCompetitorsDto>> GetCompetitionsWithoutCompetitors(CancellationToken cancellationToken = default)
    {
        var sql = _queryProvider.GetCompetitionsWithoutCompetitors();

        try
        {
            var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await _connection.QueryAsync<CompetitionWithoutCompetitorsDto>(cmd);
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch competitions without competitors");
            throw;
        }
    }

    public async Task<List<CompetitionWithoutPlaysDto>> GetCompetitionsWithoutPlays(CancellationToken cancellationToken = default)
    {
        var sql = _queryProvider.GetCompetitionsWithoutPlays();

        try
        {
            var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await _connection.QueryAsync<CompetitionWithoutPlaysDto>(cmd);
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch competitions without plays");
            throw;
        }
    }

    public async Task<List<CompetitionWithoutDrivesDto>> GetCompetitionsWithoutDrives(CancellationToken cancellationToken = default)
    {
        var sql = _queryProvider.GetCompetitionsWithoutDrives();

        try
        {
            var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await _connection.QueryAsync<CompetitionWithoutDrivesDto>(cmd);
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch competitions without drives");
            throw;
        }
    }

    public async Task<List<CompetitionWithoutMetricsDto>> GetCompetitionsWithoutMetrics(CancellationToken cancellationToken = default)
    {
        var sql = _queryProvider.GetCompetitionsWithoutMetrics();

        try
        {
            var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await _connection.QueryAsync<CompetitionWithoutMetricsDto>(cmd);
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch competitions without metrics");
            throw;
        }
    }
}