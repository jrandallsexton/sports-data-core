namespace SportsData.Api.Application.UI.Messageboard.Dtos;

/// <summary>
/// Represents a pagination request with validated limit bounds.
/// </summary>
/// <example>
/// <code>
/// // Create with default limit (20)
/// var pageRequest = PageRequest.Create();
/// 
/// // Create with custom limit
/// var customPage = PageRequest.Create(limit: 50, cursor: "next-page-token");
/// </code>
/// </example>
public sealed record PageRequest
{
    private const int MinLimit = 1;
    private const int MaxLimit = 1000;
    private const int DefaultLimit = 20;

    public int Limit { get; init; }
    public string? Cursor { get; init; }

    private PageRequest(int limit, string? cursor)
    {
        Limit = limit;
        Cursor = cursor;
    }

    /// <summary>
    /// Creates a new PageRequest with validated limit.
    /// </summary>
    /// <param name="limit">The number of items to return. Must be between 1 and 1000.</param>
    /// <param name="cursor">Optional cursor for pagination.</param>
    /// <returns>A new PageRequest instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when limit is not between 1 and 1000.</exception>
    public static PageRequest Create(int limit = DefaultLimit, string? cursor = null)
    {
        if (limit < MinLimit || limit > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                $"Limit must be between {MinLimit} and {MaxLimit}.");
        }

        return new PageRequest(limit, cursor);
    }
}
