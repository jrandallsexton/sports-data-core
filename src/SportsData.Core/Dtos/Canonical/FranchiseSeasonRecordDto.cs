using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical;

public record FranchiseSeasonRecordDto : DtoBase
{
    public Guid FranchiseSeasonId { get; init; }

    public Guid FranchiseId { get; init; }

    public int SeasonYear { get; init; }

    public string RecordId { get; init; } = null!;

    public string Name { get; init; } = null!;

    public string? Abbreviation { get; init; }

    public string? DisplayName { get; init; }

    public string? ShortDisplayName { get; init; }

    public string? Description { get; init; }

    public string Type { get; init; } = null!;

    public string Summary { get; init; } = null!;

    public string DisplayValue { get; init; } = null!;

    public double Value { get; init; }

    public List<FranchiseSeasonRecordStatDto> Stats { get; init; } = [];
}