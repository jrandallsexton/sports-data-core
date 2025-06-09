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

    public string Abbreviation { get; init; } = null!;

    public string DisplayName { get; init; } = null!;

    public string ShortDisplayName { get; init; } = null!;

    public string Description { get; init; } = null!;

    public string Type { get; init; } = null!;

    public string Summary { get; init; } = null!;

    public string DisplayValue { get; init; } = null!;

    public double Value { get; init; }

    public List<FranchiseSeasonRecordStatDto> Stats { get; init; } = [];
}