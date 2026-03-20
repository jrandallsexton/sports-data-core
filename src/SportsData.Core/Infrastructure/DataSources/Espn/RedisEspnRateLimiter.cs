using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public class RedisEspnRateLimiter : IEspnRateLimiter
    {
        private const string BucketKey = "espn:ratelimit:bucket";
        private const int PollIntervalMs = 100;

        // Lua script: refill tokens based on elapsed time, then try to consume one.
        // KEYS[1] = bucket hash key
        // ARGV[1] = max tokens (burst capacity)
        // ARGV[2] = tokens per second (refill rate)
        // ARGV[3] = current time in milliseconds
        // ARGV[4] = TTL in seconds (safety net)
        // Returns 1 if token acquired, 0 if denied.
        private const string TokenBucketLua = @"
local key = KEYS[1]
local max_tokens = tonumber(ARGV[1])
local tokens_per_sec = tonumber(ARGV[2])
local now_ms = tonumber(ARGV[3])
local ttl_sec = tonumber(ARGV[4])

local tokens = tonumber(redis.call('HGET', key, 'tokens'))
local last_refill = tonumber(redis.call('HGET', key, 'last_refill'))

if tokens == nil or last_refill == nil then
    -- Initialize bucket
    tokens = max_tokens
    last_refill = now_ms
end

-- Refill based on elapsed time
local elapsed_ms = now_ms - last_refill
if elapsed_ms > 0 then
    local refill = elapsed_ms / 1000.0 * tokens_per_sec
    tokens = math.min(max_tokens, tokens + refill)
    last_refill = now_ms
end

-- Try to consume one token
if tokens >= 1 then
    tokens = tokens - 1
    redis.call('HSET', key, 'tokens', tostring(tokens), 'last_refill', tostring(last_refill))
    redis.call('EXPIRE', key, ttl_sec)
    return 1
else
    redis.call('HSET', key, 'tokens', tostring(tokens), 'last_refill', tostring(last_refill))
    redis.call('EXPIRE', key, ttl_sec)
    return 0
end
";

        private readonly IConnectionMultiplexer _redis;
        private readonly IOptionsMonitor<EspnApiClientConfig> _configMonitor;
        private readonly ILogger<RedisEspnRateLimiter> _logger;

        public RedisEspnRateLimiter(
            IConnectionMultiplexer redis,
            IOptionsMonitor<EspnApiClientConfig> config,
            ILogger<RedisEspnRateLimiter> logger)
        {
            _redis = redis;
            _configMonitor = config;
            _logger = logger;
        }

        public async Task<bool> AcquireAsync(CancellationToken ct = default)
        {
            var cfg = _configMonitor.CurrentValue;
            var maxTokens = Math.Max(1, cfg.RateLimitMaxTokens);
            var tokensPerSecond = cfg.RateLimitTokensPerSecond > 0 ? cfg.RateLimitTokensPerSecond : 1.0;
            var maxWaitMs = cfg.RateLimitMaxWaitMs;
            var bucketTtlSeconds = (int)Math.Ceiling(maxTokens / tokensPerSecond) + 60;

            var sw = Stopwatch.StartNew();

            try
            {
                var db = _redis.GetDatabase();

                while (sw.ElapsedMilliseconds < maxWaitMs)
                {
                    ct.ThrowIfCancellationRequested();

                    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    var result = (int)await db.ScriptEvaluateAsync(
                        TokenBucketLua,
                        new RedisKey[] { BucketKey },
                        new RedisValue[] { maxTokens, tokensPerSecond, nowMs, bucketTtlSeconds });

                    if (result == 1)
                    {
                        if (sw.ElapsedMilliseconds > PollIntervalMs)
                        {
                            _logger.LogDebug(
                                "ESPN rate limiter: acquired token after waiting {WaitMs}ms",
                                sw.ElapsedMilliseconds);
                        }

                        return true;
                    }

                    await Task.Delay(PollIntervalMs, ct);
                }

                // Max wait exceeded — fail open
                _logger.LogWarning(
                    "ESPN rate limiter: max wait {MaxWaitMs}ms exceeded, failing open",
                    maxWaitMs);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw; // Let cancellation propagate
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "ESPN rate limiter: Redis error, failing open after {ElapsedMs}ms",
                    sw.ElapsedMilliseconds);
                return true; // Fail open
            }
        }
    }
}
