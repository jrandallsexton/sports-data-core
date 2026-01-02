using Dapper;

using SportsData.Api.Infrastructure.Data.Canonical.Models;

using System.Data;

namespace SportsData.Api.Infrastructure.Data.Canonical;

public interface IProvideCanonicalAdminData
{
    /// <summary>
/// Retrieve competitions that have no associated competitors.
/// </summary>
/// <param name="cancellationToken">Token to cancel the database query.</param>
/// <returns>A list of competitions that have no associated competitors.</returns>
Task<List<CompetitionWithoutCompetitorsDto>> GetCompetitionsWithoutCompetitors(CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves competitions that have no associated plays.
/// </summary>
/// <returns>A list of <see cref="CompetitionWithoutPlaysDto"/> representing competitions with no plays.</returns>
Task<List<CompetitionWithoutPlaysDto>> GetCompetitionsWithoutPlays(CancellationToken cancellationToken = default);
    /// <summary>
/// Gets competitions that have no drive records.
/// </summary>
/// <returns>A list of competitions that have no drive records.</returns>
Task<List<CompetitionWithoutDrivesDto>> GetCompetitionsWithoutDrives(CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves competitions that have no associated metrics.
/// </summary>
/// <param name="cancellationToken">Token to cancel the database query.</param>
/// <returns>A list of competitions that have no associated metrics.</returns>
Task<List<CompetitionWithoutMetricsDto>> GetCompetitionsWithoutMetrics(CancellationToken cancellationToken = default);
}

public class CanonicalAdminDataProvider : IProvideCanonicalAdminData
{
    private readonly IDbConnection _connection;
    private readonly ILogger<CanonicalAdminDataProvider> _logger;
    private readonly CanonicalAdminDataQueryProvider _queryProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="CanonicalAdminDataProvider"/> with the required dependencies.
    /// </summary>
    /// <param name="queryProvider">Provides SQL query text used by the provider to retrieve canonical admin data.</param>
    public CanonicalAdminDataProvider(
        ILogger<CanonicalAdminDataProvider> logger,
        IDbConnection connection,
        CanonicalAdminDataQueryProvider queryProvider)
    {
        _logger = logger;
        _connection = connection;
        _queryProvider = queryProvider;
    }

    /// <summary>
    /// Fetches competitions that have no associated competitors from the canonical data store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the database query.</param>
    /// <returns>A list of <see cref="CompetitionWithoutCompetitorsDto"/> representing competitions with no competitors.</returns>
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

    /// <summary>
    /// Retrieves competitions that have no associated plays.
    /// </summary>
    /// <returns>A list of <see cref="CompetitionWithoutPlaysDto"/> representing competitions that have no plays.</returns>
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

    /// <summary>
    /// Retrieves competitions that have no drives from the canonical data store.
    /// </summary>
    /// <returns>A list of <see cref="CompetitionWithoutDrivesDto"/> for competitions that contain no drives.</returns>
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

    /// <summary>
    /// Retrieve competitions that have no associated metrics.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the database query.</param>
    /// <returns>A list of CompetitionWithoutMetricsDto representing competitions that lack metrics.</returns>
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