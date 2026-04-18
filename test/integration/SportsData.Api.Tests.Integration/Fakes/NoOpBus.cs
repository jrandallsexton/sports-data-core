using MassTransit;

using SportsData.Core.Eventing;

namespace SportsData.Api.Tests.Integration.Fakes;

/// <summary>
/// Swallows all publish/send calls. Lets handlers run without RabbitMQ / Azure Service Bus
/// reachable. Mirrors the pattern used in SportsData.Producer.Tests.Integration.
/// </summary>
public sealed class NoOpBus : IEventBus
{
    public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => new NoOpConnectHandle();

    public Task<ISendEndpoint> GetPublishSendEndpoint<T>() where T : class =>
        Task.FromResult<ISendEndpoint>(new NoOpSendEndpoint());

    public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public Task Publish<T>(T message, IDictionary<string, object> headers, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public Task PublishBatch<T>(IEnumerable<T> messages, CancellationToken ct = default) where T : class =>
        Task.CompletedTask;

    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public Task Publish(object message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public ConnectHandle ConnectSendObserver(ISendObserver observer) => new NoOpConnectHandle();

    public Task<ISendEndpoint> GetSendEndpoint(Uri address) =>
        Task.FromResult<ISendEndpoint>(new NoOpSendEndpoint());

    public ConnectHandle ConnectConsumePipe<T>(IPipe<ConsumeContext<T>> pipe) where T : class =>
        new NoOpConnectHandle();

    public ConnectHandle ConnectConsumePipe<T>(IPipe<ConsumeContext<T>> pipe, ConnectPipeOptions options) where T : class =>
        new NoOpConnectHandle();

    public ConnectHandle ConnectRequestPipe<T>(Guid requestId, IPipe<ConsumeContext<T>> pipe) where T : class =>
        new NoOpConnectHandle();

    public ConnectHandle ConnectConsumeMessageObserver<T>(IConsumeMessageObserver<T> observer) where T : class =>
        new NoOpConnectHandle();

    public ConnectHandle ConnectConsumeObserver(IConsumeObserver observer) => new NoOpConnectHandle();
    public ConnectHandle ConnectReceiveObserver(IReceiveObserver observer) => new NoOpConnectHandle();
    public ConnectHandle ConnectReceiveEndpointObserver(IReceiveEndpointObserver observer) => new NoOpConnectHandle();
    public ConnectHandle ConnectEndpointConfigurationObserver(IEndpointConfigurationObserver observer) => new NoOpConnectHandle();

    public HostReceiveEndpointHandle ConnectReceiveEndpoint(
        IEndpointDefinition definition,
        IEndpointNameFormatter endpointNameFormatter = null!,
        Action<IReceiveEndpointConfigurator> configureEndpoint = null!) =>
        new NoOpHostReceiveEndpointHandle();

    public HostReceiveEndpointHandle ConnectReceiveEndpoint(
        string queueName,
        Action<IReceiveEndpointConfigurator> configureEndpoint = null!) =>
        new NoOpHostReceiveEndpointHandle();

    public void Probe(ProbeContext context) { }

    public Uri Address => new Uri("loopback://localhost/noop");
    public IBusTopology Topology => new NoOpBusTopology();

    private sealed class NoOpSendEndpoint : ISendEndpoint
    {
        public Task Send<T>(T message, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public Task Send<T>(T message, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public Task Send<T>(T message, IPipe<SendContext> pipe, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public Task Send(object message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Send(object message, Type messageType, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Send(object message, IPipe<SendContext> pipe, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Send(object message, Type messageType, IPipe<SendContext> pipe, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Send<T>(object values, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public Task Send<T>(object values, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public Task Send<T>(object values, IPipe<SendContext> pipe, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public ConnectHandle ConnectSendObserver(ISendObserver observer) => new NoOpConnectHandle();
    }

    private sealed class NoOpConnectHandle : ConnectHandle
    {
        public void Disconnect() { }
        public Task DisconnectAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class NoOpHostReceiveEndpointHandle : HostReceiveEndpointHandle
    {
        public Task Ready => Task.CompletedTask;
        public Task Completed => Task.CompletedTask;
        public void Disconnect() { }
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReceiveEndpoint ReceiveEndpoint => null!;
        Task<ReceiveEndpointReady> HostReceiveEndpointHandle.Ready => Task.FromResult<ReceiveEndpointReady>(null!);
    }

    private sealed class NoOpBusTopology : IBusTopology
    {
        public IMessagePublishTopology<T> Publish<T>() where T : class => throw new NotImplementedException();
        public IMessageSendTopology<T> Send<T>() where T : class => throw new NotImplementedException();
        public IMessageTopology<T> Message<T>() where T : class => throw new NotImplementedException();
        public bool TryGetPublishAddress(Type messageType, out Uri publishAddress) { publishAddress = null!; return false; }
        public bool TryGetPublishAddress<T>(out Uri publishAddress) where T : class { publishAddress = null!; return false; }
        public IPublishTopology PublishTopology => null!;
        public ISendTopology SendTopology => null!;
    }
}

public sealed class NoOpPublishEndpoint : IPublishEndpoint
{
    public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => new EmptyHandle();
    public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    public Task Publish(object message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;

    private sealed class EmptyHandle : ConnectHandle
    {
        public void Disconnect() { }
        public Task DisconnectAsync() => Task.CompletedTask;
        public void Dispose() { }
    }
}
