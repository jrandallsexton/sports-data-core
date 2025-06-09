using System;

namespace SportsData.Core.Dtos.Canonical;

public record FranchiseSeasonRecordStatDto : DtoBase
{
    public Guid FranchiseSeasonRecordId { get; init; }

    public string Name { get; init; } = null!;

    public string DisplayName { get; init; } = null!;

    public string ShortDisplayName { get; init; } = null!;

    public string Description { get; init; } = null!;

    public string Abbreviation { get; init; } = null!;

    public string Type { get; init; } = null!;

    public double Value { get; init; }

    public string DisplayValue { get; init; } = null!;
}