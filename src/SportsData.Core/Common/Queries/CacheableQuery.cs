using MediatR;

namespace SportsData.Core.Common.Queries;

public class CacheableQuery<T> : IRequest<T>
{
    public bool BypassCache { get; set; }
}