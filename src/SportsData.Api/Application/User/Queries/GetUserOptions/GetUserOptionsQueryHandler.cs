using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.User.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.User.Queries.GetUserOptions;

public interface IGetUserOptionsQueryHandler
{
    Task<Result<UserOptionsDto>> ExecuteAsync(
        GetUserOptionsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Projects a user's <c>UserOption</c> key/value rows into the typed
/// <c>UserOptionsDto</c>. Unknown keys are ignored; absent or unparsable
/// values fall back to each option's default, so the client always gets a
/// full set. See docs/features/user-options.md.
/// </summary>
public class GetUserOptionsQueryHandler : IGetUserOptionsQueryHandler
{
    private readonly AppDataContext _db;
    private readonly ILogger<GetUserOptionsQueryHandler> _logger;

    public GetUserOptionsQueryHandler(
        AppDataContext db,
        ILogger<GetUserOptionsQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<UserOptionsDto>> ExecuteAsync(
        GetUserOptionsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting user options for UserId={UserId}", query.UserId);

        var rows = await _db.UserOptions
            .AsNoTracking()
            .Where(o => o.UserId == query.UserId)
            .Select(o => new { o.Key, o.Value })
            .ToListAsync(cancellationToken);

        var byKey = rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

        return new Success<UserOptionsDto>(new UserOptionsDto
        {
            ShowGamblingContent = ParseBool(byKey, UserOptionKeys.ShowGamblingContent, defaultValue: false)
        });
    }

    private static bool ParseBool(
        IReadOnlyDictionary<string, string> byKey,
        string key,
        bool defaultValue)
        => byKey.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
}
