using SportsData.Core.Common;

using System;

namespace SportsData.Core.Dtos.Canonical;

public record ContestDto : DtoBase
{
    public string Name { get; init; } = null!;

    public string ShortName { get; init; } = null!;

    public DateTime StartDateUtc { get; init; }

    public DateTime? EndDateUtc { get; init; }

    public ContestStatus Status { get; init; }

    public Sport Sport { get; init; }

    public int SeasonYear { get; init; }

    public int? Week { get; init; }

    public bool? NeutralSite { get; init; }

    public int? Attendance { get; init; }

    public string? EventNote { get; init; }

    public Guid? VenueId { get; init; }
}