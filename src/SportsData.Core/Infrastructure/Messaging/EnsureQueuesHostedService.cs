using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

using SportsData.Core.Config;

namespace SportsData.Core.Infrastructure.Messaging;

/// <summary>
/// Declares RabbitMQ queues and exchange bindings at startup to ensure they exist
/// even when their consumers are disabled. Prevents message loss when events are
/// published to exchanges with no bound queue (e.g., the dead-letter queue used
/// for DLQ replay).
/// </summary>
public class EnsureQueuesHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnsureQueuesHostedService> _logger;

    /// <summary>
    /// Queues that must exist for the replay/DLQ pattern to work.
    /// Exchange names follow MassTransit's convention: full CLR type name with ':' separator.
    /// </summary>
    private static readonly (string Queue, string Exchange)[] RequiredQueues =
    [
        ("document-dead-letter", "SportsData.Core.Eventing.Events.Documents:DocumentDeadLetter")
    ];

    public EnsureQueuesHostedService(
        IConfiguration configuration,
        ILogger<EnsureQueuesHostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var useRabbitMq = _configuration.GetValue<bool>(CommonConfigKeys.MessagingUseRabbitMq);
        if (!useRabbitMq) return;

        var host = _configuration[CommonConfigKeys.RabbitMqHost];
        var username = _configuration[CommonConfigKeys.RabbitMqUsername];
        var password = _configuration[CommonConfigKeys.RabbitMqPassword];

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("RabbitMQ credentials not fully configured — skipping queue declaration");
            return;
        }

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = host,
                UserName = username,
                Password = password
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            foreach (var (queue, exchange) in RequiredQueues)
            {
                await channel.QueueDeclareAsync(queue, durable: true, exclusive: false,
                    autoDelete: false, cancellationToken: cancellationToken);

                await channel.ExchangeDeclareAsync(exchange, "fanout", durable: true,
                    autoDelete: false, cancellationToken: cancellationToken);

                await channel.QueueBindAsync(queue, exchange, routingKey: "",
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Ensured queue {Queue} bound to exchange {Exchange}", queue, exchange);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to declare required queues — they may be created later by consumers");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
