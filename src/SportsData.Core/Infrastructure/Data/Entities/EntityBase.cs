using SportsData.Core.Common.Hashing;

using System;
using System.ComponentModel.DataAnnotations;

namespace SportsData.Core.Infrastructure.Data.Entities
{
    public abstract class EntityBase<T> : IEntity<T>, IHasSourceUrlHash
    {
        [Key]
        public required T Id { get; set; }

        /// <summary>
        /// Identifier of the canonical entity this entity is associated with, if any.
        /// </summary>
        public Guid? CanonicalId { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedUtc { get; set; }

        public Guid CreatedBy { get; set; }

        public Guid? ModifiedBy { get; set; }

        public DateTime LastModified => ModifiedUtc ?? CreatedUtc;

        public required string UrlHash { get; set; }
    }
}
