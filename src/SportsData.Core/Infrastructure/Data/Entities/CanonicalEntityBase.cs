﻿using SportsData.Core.Common.Hashing;

using System;
using System.ComponentModel.DataAnnotations;

namespace SportsData.Core.Infrastructure.Data.Entities;

public abstract class CanonicalEntityBase<T> : IEntity<T>, IHasSourceUrlHash
{
    [Key]
    public required T Id { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedUtc { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? ModifiedBy { get; set; }

    public DateTime LastModified => ModifiedUtc ?? CreatedUtc;

    public string UrlHash { get; set; }
}