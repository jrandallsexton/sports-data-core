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

                    services.AddLocalServices(mode);
                });

            var host = builder.Build();
            Services = host.Services;

            await Task.CompletedTask;
        }

        public Task DisposeAsync() => Task.CompletedTask;
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
