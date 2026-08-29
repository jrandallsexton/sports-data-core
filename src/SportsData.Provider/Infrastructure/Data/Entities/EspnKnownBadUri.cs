using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using System;

namespace SportsData.Provider.Infrastructure.Data.Entities
{
    /// <summary>
    /// An ESPN URI that returned 400 BadRequest — a permanent "unsupported
    /// resource" verdict (e.g. "Probabilities are not supported for ...
    /// competition: X"). Persisted so the suppression survives pod restarts
    /// and KEDA scale-ups: <see cref="Providers.Espn.KnownBadUriCache"/>
    /// hydrates from this table at startup and writes through on new 400s.
    /// Rows expire via <see cref="ExpiresUtc"/> so a resource that gains
    /// support is eventually re-probed. Doubles as an operator view of
    /// everything ESPN refuses to serve.
    /// </summary>
    public class EspnKnownBadUri
    {
        /// <summary>
        /// SHA-256 of the normalized URL (scheme+host+path, lowercased, no
        /// query string) via <c>HashProvider.GenerateHashFromUri</c> — query
        /// variants (paging, lang/region) collapse to one row.
        /// </summary>
        public required string UrlHash { get; set; }

        public required Uri Uri { get; set; }

        /// <summary>"BadRequest" (permanent, flat TTL) or "NotFound" (escalating backoff).</summary>
        public string Reason { get; set; } = "BadRequest";

        /// <summary>
        /// Consecutive failures — drives the NotFound backoff (5m doubling
        /// to a 6h cap). Persisted so a fresh pod continues the escalation
        /// instead of restarting it at 5 minutes.
        /// </summary>
        public int FailureCount { get; set; } = 1;

        public DateTime CreatedUtc { get; set; }

        public DateTime ExpiresUtc { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<EspnKnownBadUri>
        {
            public void Configure(EntityTypeBuilder<EspnKnownBadUri> builder)
            {
                builder.ToTable(nameof(EspnKnownBadUri));

                builder.HasKey(t => t.UrlHash);

                builder.Property(x => x.UrlHash)
                    .HasMaxLength(64);

                builder.Property(p => p.Uri)
                    .HasMaxLength(512);

                builder.Property(x => x.Reason)
                    .HasMaxLength(16);
            }
        }
    }
}
