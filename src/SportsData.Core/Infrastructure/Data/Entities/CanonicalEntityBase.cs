using System;
using System.ComponentModel.DataAnnotations;

namespace SportsData.Core.Infrastructure.Data.Entities;

/// <summary>
/// Used as a base for ALL canonical entities that have an Id and audit fields.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class CanonicalEntityBase<T> : IEntity<T>
{
    [Key]
    public T? Id { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedUtc { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? ModifiedBy { get; set; }

    public DateTime LastModified => ModifiedUtc ?? CreatedUtc;
}