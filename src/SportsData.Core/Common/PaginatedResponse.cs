using System;
using System.Collections.Generic;

namespace SportsData.Core.Common;

/// <summary>
/// Base class for paginated API responses with HATEOAS navigation links.
/// Provides a consistent structure for all paginated endpoints across all services.
/// </summary>
/// <typeparam name="T">The type of items in the paginated collection</typeparam>
public class PaginatedResponse<T>
{
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
    
    /// <summary>
    /// HATEOAS links for pagination navigation (self, first, last, prev, next)
    /// </summary>
    public Dictionary<string, Uri> Links { get; set; } = new();

    /// <summary>
    /// The paginated collection of items
    /// </summary>
    public List<T> Items { get; set; } = [];
}
