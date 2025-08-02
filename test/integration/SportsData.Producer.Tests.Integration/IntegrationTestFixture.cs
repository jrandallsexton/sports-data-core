using MassTransit;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.DependencyInjection;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Mapping;

using System.Reflection;

using Xunit;

namespace SportsData.Producer.Tests.Integration
{
    public class IntegrationTestFixture : IAsyncLifetime
    {
        public IServiceProvider Services { get; private set; } = null!;
        private IConfiguration _configuration = null!;

        public async Task InitializeAsync()
        {
            var mode = Sport.FootballNcaa;

            var config = new ConfigurationManager(); // this type supports AddCommonConfiguration
            config.AddEnvironmentVariables();
            config.AddCommonConfiguration("Development", "SportsData.Producer", mode);

            _configuration = config;

            var builder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConfiguration>(_configuration);

                    services.AddCoreServices(_configuration, mode);
                    services.AddClients(_configuration);

                    services.AddDataPersistence<FootballDataContext>(_configuration, "SportsData.Producer", mode);
                    services.AddScoped<TeamSportDataContext, FootballDataContext>();
                    services.AddScoped<BaseDataContext, FootballDataContext>();

                    services.AddAutoMapper(typeof(MappingProfile));
                    services.AddMediatR(Assembly.GetExecutingAssembly());
                    services.AddScoped<IPublishEndpoint, NoOpPublishEndpoint>();
                    services.AddScoped<IBus, NoOpBus>();

                    services.AddLocalServices(mode);
                });

            var host = builder.Build();
            Services = host.Services;

            await Task.CompletedTask;
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    public class NoOpBus : IBus
    {
        public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => new NoOpConnectHandle();

        public Task<ISendEndpoint> GetPublishSendEndpoint<T>() where T : class =>
            Task.FromResult<ISendEndpoint>(new NoOpSendEndpoint());

        public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class =>
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
            IEndpointNameFormatter endpointNameFormatter = null,
            Action<IReceiveEndpointConfigurator> configureEndpoint = null) =>
            new NoOpHostReceiveEndpointHandle();

        public HostReceiveEndpointHandle ConnectReceiveEndpoint(
            string queueName,
            Action<IReceiveEndpointConfigurator> configureEndpoint = null) =>
            new NoOpHostReceiveEndpointHandle();

        public void Probe(ProbeContext context) { }

        public Uri Address => new Uri("loopback://localhost/noop");
        public IBusTopology Topology => new NoOpBusTopology();

        // --- NoOp Helpers ---

        private class NoOpSendEndpoint : ISendEndpoint
        {
            public Task Send<T>(T message, CancellationToken cancellationToken = default) where T : class =>
                Task.CompletedTask;

            public Task Send<T>(T message, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken = default) where T : class =>
                Task.CompletedTask;

            public Task Send<T>(T message, IPipe<SendContext> pipe, CancellationToken cancellationToken = default) where T : class =>
                Task.CompletedTask;

            public Task Send(object message, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task Send(object message, Type messageType, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task Send(object message, IPipe<SendContext> pipe, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task Send(object message, Type messageType, IPipe<SendContext> pipe, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task Send<T>(object values, CancellationToken cancellationToken = new CancellationToken()) where T : class =>
                Task.CompletedTask;

            public Task Send<T>(object values, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken = new CancellationToken()) where T : class =>
                Task.CompletedTask;

            public Task Send<T>(object values, IPipe<SendContext> pipe, CancellationToken cancellationToken = new CancellationToken()) where T : class =>
                Task.CompletedTask;

            public ConnectHandle ConnectSendObserver(ISendObserver observer)
            {
                throw new NotImplementedException();
            }
        }

        private class NoOpConnectHandle : ConnectHandle
        {
            public void Disconnect() { }
            public Task DisconnectAsync() => Task.CompletedTask;
            public void Dispose()
            {

            }
        }

        private class NoOpHostReceiveEndpointHandle : HostReceiveEndpointHandle
        {
            private Task<ReceiveEndpointReady> _ready;
            public Task Ready => Task.CompletedTask;
            public Task Completed => Task.CompletedTask;
            public void Disconnect() { }
            public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public IReceiveEndpoint ReceiveEndpoint { get; }

            Task<ReceiveEndpointReady> HostReceiveEndpointHandle.Ready => _ready;
        }

        private class NoOpBusTopology : IBusTopology
        {
            public IMessagePublishTopology<T> Publish<T>() where T : class
            {
                throw new NotImplementedException();
            }

            public IMessageSendTopology<T> Send<T>() where T : class
            {
                throw new NotImplementedException();
            }

            public IMessageTopology<T> Message<T>() where T : class
            {
                throw new NotImplementedException();
            }

            public bool TryGetPublishAddress(Type messageType, out Uri publishAddress)
            {
                throw new NotImplementedException();
            }

            public bool TryGetPublishAddress<T>(out Uri publishAddress) where T : class
            {
                throw new NotImplementedException();
            }

            public IPublishTopology PublishTopology => null!;
            public ISendTopology SendTopology => null!;
        }
    }

    public class NoOpPublishEndpoint : IPublishEndpoint
    {
        public ConnectHandle ConnectPublishObserver(IPublishObserver observer)
        {
            // No-op observer connection
            return new EmptyConnectHandle();
        }

        public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
            => Task.CompletedTask;

        public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
            => Task.CompletedTask;

        public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
            => Task.CompletedTask;

        public Task Publish(object message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class
            => Task.CompletedTask;

        public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
            => Task.CompletedTask;

        public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
            => Task.CompletedTask;

        private class EmptyConnectHandle : ConnectHandle
        {
            public void Disconnect() { }
            public void Dispose() { }
            public bool IsConnected => false;
        }
    }
}
