using MassTransit;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Eventing
{
    public interface IEventBus
    {
        Task Publish<T>(T message, CancellationToken ct = default) where T : class;

        // Batch publish (fire-and-forget semantics like MT PublishBatch)
        Task PublishBatch<T>(IEnumerable<T> messages, CancellationToken ct = default) where T : class;
    }

    public static class EventBusExtensions
    {
        public static Task PublishBatch<T>(this IEventBus bus, CancellationToken ct = default, params T[] messages)
            where T : class
            => bus.PublishBatch((IEnumerable<T>)messages, ct);
    }

    public enum DeliveryMode { Default, Direct }

    public interface IMessageDeliveryPolicy
    {
        DeliveryMode Mode { get; }
    }

    public interface IMessageDeliveryScope
    {
        IDisposable Use(DeliveryMode mode); // returns a scope you dispose
    }

    public sealed class MessageDeliveryPolicy : IMessageDeliveryPolicy, IMessageDeliveryScope
    {
        private static readonly AsyncLocal<DeliveryMode?> _current = new();

        public DeliveryMode Mode => _current.Value ?? DeliveryMode.Default;

        public IDisposable Use(DeliveryMode mode)
        {
            var prior = _current.Value;
            _current.Value = mode;
            return new Scope(() => _current.Value = prior);
        }

        sealed class Scope : IDisposable
        {
            private readonly Action _onDispose;
            public Scope(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }

    public sealed class EventBusAdapter : IEventBus
    {
        private readonly IPublishEndpoint _publish; // outbox-intercepted
        private readonly IBus _bus;                 // direct
        private readonly IMessageDeliveryPolicy _policy;
        private readonly IOutboxAmbientState _outbox;

        public EventBusAdapter(IPublishEndpoint publish, IBus bus,
            IMessageDeliveryPolicy policy, IOutboxAmbientState outbox)
        { _publish = publish; _bus = bus; _policy = policy; _outbox = outbox; }

        public async Task Publish<T>(T message, CancellationToken ct = default) where T : class
        {
            var direct = _policy.Mode == DeliveryMode.Direct || !_outbox.IsActive;

            if (direct)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct); // 🚨 Dirty Hack: Give consumers time to commit
                await _bus.Publish(message, ct);
            }
            else
            {
                await _publish.Publish(message, ct);
            }
        }

        public Task PublishBatch<T>(IEnumerable<T> messages, CancellationToken ct = default) where T : class
        {
            var direct = _policy.Mode == DeliveryMode.Direct || !_outbox.IsActive;

            // Keep it simple & backpressure-friendly: chunk to avoid giant fan-out
            const int chunkSize = 256;

            return PublishInChunks(messages, chunkSize, direct, ct);
        }

        private async Task PublishInChunks<T>(IEnumerable<T> messages, int chunkSize, bool direct, CancellationToken ct)
            where T : class
        {
            var buffer = new List<T>(chunkSize);
            foreach (var m in messages)
            {
                buffer.Add(m);
                if (buffer.Count == chunkSize)
                {
                    await Flush(buffer, direct, ct);
                    buffer.Clear();
                }
            }
            if (buffer.Count > 0)
                await Flush(buffer, direct, ct);

            async Task Flush(List<T> batch, bool useDirect, CancellationToken token)
            {
                if (useDirect)
                    await Task.WhenAll(batch.Select(x => _bus.Publish(x, token)));
                else
                    await Task.WhenAll(batch.Select(x => _publish.Publish(x, token)));
            }
        }
    }

    public interface IOutboxAmbientState { bool IsActive { get; } }

    public sealed class EfOutboxAmbientState<TDb> : IOutboxAmbientState where TDb : DbContext
    {
        private readonly TDb _db;
        public EfOutboxAmbientState(TDb db) => _db = db;
        public bool IsActive => true;
    }

}