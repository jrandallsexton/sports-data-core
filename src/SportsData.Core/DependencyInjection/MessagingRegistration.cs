using MassTransit;
using MassTransit.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Config;
using SportsData.Core.Eventing; // <- IEventBus, EventBusAdapter, MessageDeliveryPolicy, IOutboxAmbientState, EfOutboxAmbientState<TDb>

using System;
using System.Collections.Generic;
using System.Data; // Add this for IsolationLevel

namespace SportsData.Core.DependencyInjection
{
    public static class MessagingRegistration
    {
        /* NEW */
        public static IServiceCollection AddMessaging<T1, T2, T3>(
            this IServiceCollection services,
            IConfiguration config,
            List<Type> consumers)
            where T1 : DbContext
            where T2 : DbContext
            where T3 : DbContext
        {
            // Register outboxes for each context
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                consumers?.ForEach(z => x.AddConsumer(z));

                x.AddEntityFrameworkOutbox<T1>(o =>
                {
                    o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
                    o.QueryDelay = TimeSpan.FromSeconds(1);
                    o.IsolationLevel = IsolationLevel.ReadCommitted; // Fix PostgreSQL serialization conflicts
                    o.UsePostgres()
                        .UseBusOutbox(busOutbox =>
                        {
                            busOutbox.MessageDeliveryLimit = 1000;
                        });
                });

                x.AddEntityFrameworkOutbox<T2>(o =>
                {
                    o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
                    o.QueryDelay = TimeSpan.FromSeconds(1);
                    o.IsolationLevel = IsolationLevel.ReadCommitted; // Fix PostgreSQL serialization conflicts
                    o.UsePostgres()
                        .UseBusOutbox(busOutbox =>
                        {
                            busOutbox.MessageDeliveryLimit = 1000;
                        });
                });

                x.AddEntityFrameworkOutbox<T3>(o =>
                {
                    o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
                    o.QueryDelay = TimeSpan.FromSeconds(1);
                    o.IsolationLevel = IsolationLevel.ReadCommitted; // Fix PostgreSQL serialization conflicts
                    o.UsePostgres()
                        .UseBusOutbox(busOutbox =>
                        {
                            busOutbox.MessageDeliveryLimit = 1000;
                        });
                });

                // Check if RabbitMQ mode is enabled
                var useRabbitMq = config.GetValue<bool>(CommonConfigKeys.MessagingUseRabbitMq);
                
                if (useRabbitMq)
                {
                    var rabbitHost = config[CommonConfigKeys.RabbitMqHost];
                    var rabbitUsername = config[CommonConfigKeys.RabbitMqUsername];
                    var rabbitPassword = config[CommonConfigKeys.RabbitMqPassword];

                    if (string.IsNullOrWhiteSpace(rabbitHost))
                    {
                        throw new InvalidOperationException(
                            $"RabbitMQ is enabled but {CommonConfigKeys.RabbitMqHost} is not configured. "
                            + "Please set the RabbitMQ host in configuration.");
                    }

                    if (string.IsNullOrWhiteSpace(rabbitUsername))
                    {
                        throw new InvalidOperationException(
                            $"RabbitMQ is enabled but {CommonConfigKeys.RabbitMqUsername} is not configured. "
                            + "Please set the RabbitMQ username in configuration.");
                    }

                    if (string.IsNullOrWhiteSpace(rabbitPassword))
                    {
                        throw new InvalidOperationException(
                            $"RabbitMQ is enabled but {CommonConfigKeys.RabbitMqPassword} is not configured. "
                            + "Please set the RabbitMQ password in configuration.");
                    }

                    x.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host(rabbitHost, "/", h =>
                        {
                            h.Username(rabbitUsername);
                            h.Password(rabbitPassword);
                        });
                        cfg.ConfigureJsonSerializerOptions(o =>
                        {
                            o.IncludeFields = true;
                            return o;
                        });
                        cfg.ConfigureEndpoints(context);
                    });

                    Console.WriteLine($"Using RabbitMQ: {rabbitHost}");
                }
                else
                {
                    var sbConnString = config[CommonConfigKeys.AzureServiceBus];
                    if (string.IsNullOrWhiteSpace(sbConnString))
                    {
                        throw new InvalidOperationException(
                            $"Azure Service Bus is enabled but {CommonConfigKeys.AzureServiceBus} is not configured. "
                            + "Please set the Azure Service Bus connection string in configuration.");
                    }

                    x.UsingAzureServiceBus((context, cfg) =>
                    {
                        cfg.Host(sbConnString);
                        cfg.ConfigureJsonSerializerOptions(o =>
                        {
                            o.IncludeFields = true;
                            return o;
                        });
                        cfg.ConfigureEndpoints(context);
                    });

                    Console.WriteLine($"Using Azure Service Bus");
                }
            });

            // Register ambient state for each context
            services.AddScoped<EfOutboxAmbientState<T1>>();
            services.AddScoped<EfOutboxAmbientState<T2>>();
            services.AddScoped<EfOutboxAmbientState<T3>>();

            services.AddScoped<IOutboxAmbientStateResolver, MultiContextOutboxResolver>();
            services.AddScoped<IOutboxAmbientState>(sp =>
                    sp.GetRequiredService<EfOutboxAmbientState<T1>>() // Default
            );

            services.AddSingleton<IMessageDeliveryPolicy, MessageDeliveryPolicy>();
            services.AddSingleton<IMessageDeliveryScope, MessageDeliveryPolicy>();
            services.AddScoped<IEventBus, EventBusAdapter>();

            return services;
        }

        private static void ConfigureOutbox<T>(EntityFrameworkOutboxConfigurator<T> o)
            where T : DbContext
        {
            o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
            o.QueryDelay = TimeSpan.FromSeconds(1);
            o.UsePostgres()
                .UseBusOutbox(busOutbox =>
                {
                    busOutbox.MessageDeliveryLimit = int.MaxValue;
                });
        }

        // Extract shared service registration
        private static IServiceCollection AddCoreMessagingServices(this IServiceCollection services)
        {
            // Only add these once — outside the per-context loop
            services.AddSingleton<IMessageDeliveryPolicy, MessageDeliveryPolicy>();
            services.AddSingleton<IMessageDeliveryScope, MessageDeliveryPolicy>();
            services.AddScoped<IEventBus, EventBusAdapter>();

            return services;
        }
        /* END NEW */

        // Non-generic overload: direct publish only (Provider, etc.)
        public static IServiceCollection AddMessaging(
            this IServiceCollection services,
            IConfiguration config,
            List<Type>? consumers)
        {
            return services.AddMessaging<DbContext>(config, consumers, useOutbox: false);
        }

        // Generic overload: with EF outbox (Producer, etc.)
        public static IServiceCollection AddMessaging<TDbContext>(
            this IServiceCollection services,
            IConfiguration config,
            List<Type>? consumers)
            where TDbContext : DbContext
        {
            return services.AddMessaging<TDbContext>(config, consumers, useOutbox: true);
        }

        // Internal method implementing both
        private static IServiceCollection AddMessaging<TDbContext>(
            this IServiceCollection services,
            IConfiguration config,
            List<Type>? consumers,
            bool useOutbox)
            where TDbContext : DbContext
        {
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();

                // register any consumers provided
                consumers?.ForEach(z => x.AddConsumer(z));

                if (useOutbox)
                {
                    x.AddEntityFrameworkOutbox<TDbContext>(o =>
                    {
                        o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
                        o.QueryDelay = TimeSpan.FromSeconds(1);
                        o.IsolationLevel = IsolationLevel.ReadCommitted; // Fix PostgreSQL serialization conflicts
                        o.UsePostgres()
                            .UseBusOutbox(busOutbox =>
                            {
                                busOutbox.MessageDeliveryLimit = int.MaxValue;
                            });
                    });
                }

                // Check if RabbitMQ mode is enabled
                var useRabbitMq = config.GetValue<bool>(CommonConfigKeys.MessagingUseRabbitMq);
                
                if (useRabbitMq)
                {
                    var rabbitHost = config[CommonConfigKeys.RabbitMqHost];
                    var rabbitUsername = config[CommonConfigKeys.RabbitMqUsername];
                    var rabbitPassword = config[CommonConfigKeys.RabbitMqPassword];

                    if (string.IsNullOrWhiteSpace(rabbitHost))
                    {
                        throw new InvalidOperationException(
                            $"RabbitMQ is enabled but {CommonConfigKeys.RabbitMqHost} is not configured. "
                            + "Please set the RabbitMQ host in configuration.");
                    }

                    if (string.IsNullOrWhiteSpace(rabbitUsername))
                    {
                        throw new InvalidOperationException(
                            $"RabbitMQ is enabled but {CommonConfigKeys.RabbitMqUsername} is not configured. "
                            + "Please set the RabbitMQ username in configuration.");
                    }

                    if (string.IsNullOrWhiteSpace(rabbitPassword))
                    {
                        throw new InvalidOperationException(
                            $"RabbitMQ is enabled but {CommonConfigKeys.RabbitMqPassword} is not configured. "
                            + "Please set the RabbitMQ password in configuration.");
                    }

                    x.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host(rabbitHost, "/", h =>
                        {
                            h.Username(rabbitUsername);
                            h.Password(rabbitPassword);
                        });
                        cfg.ConfigureJsonSerializerOptions(o =>
                        {
                            o.IncludeFields = true;
                            return o;
                        });
                        cfg.ConfigureEndpoints(context);
                    });

                    Console.WriteLine($"Using RabbitMQ: {rabbitHost}");
                }
                else
                {
                    var sbConnString = config[CommonConfigKeys.AzureServiceBus];
                    if (string.IsNullOrWhiteSpace(sbConnString))
                    {
                        throw new InvalidOperationException(
                            $"Azure Service Bus is enabled but {CommonConfigKeys.AzureServiceBus} is not configured. "
                            + "Please set the Azure Service Bus connection string in configuration.");
                    }

                    x.UsingAzureServiceBus((context, cfg) =>
                    {
                        cfg.Host(sbConnString);
                        cfg.ConfigureJsonSerializerOptions(o =>
                        {
                            o.IncludeFields = true;
                            return o;
                        });
                        cfg.ConfigureEndpoints(context);
                    });

                    Console.WriteLine($"Using Azure Service Bus");
                }

            });

            // --- NEW: wire the abstraction + delivery policy + outbox ambient state ---

            // Message delivery policy/scope (Ambient). Singleton is correct; it uses AsyncLocal internally.
            services.AddSingleton<IMessageDeliveryPolicy, MessageDeliveryPolicy>();
            services.AddSingleton<IMessageDeliveryScope, MessageDeliveryPolicy>();

            // Outbox ambient state: EF-backed when using outbox; otherwise a no-op that reports inactive.
            if (useOutbox)
            {
                services.AddScoped<IOutboxAmbientState, EfOutboxAmbientState<TDbContext>>();
            }
            else
            {
                services.AddScoped<IOutboxAmbientState, NoOutboxAmbientState>();
            }

            // Event bus adapter used by processors
            services.AddScoped<IEventBus, EventBusAdapter>();

            return services;
        }
    }

    // Simple no-op ambient state for services that don't use the EF outbox (e.g., Provider)
    internal sealed class NoOutboxAmbientState : IOutboxAmbientState
    {
        public bool IsActive => false;
    }
}
