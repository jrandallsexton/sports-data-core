using MediatR;

using System;

namespace SportsData.Core.Common.Commands;

public abstract class TrackableCommand<T> : IRequest<T>
{
    public Guid CorrelationId { get; set; }
    public Guid CausationId { get; set; }
}