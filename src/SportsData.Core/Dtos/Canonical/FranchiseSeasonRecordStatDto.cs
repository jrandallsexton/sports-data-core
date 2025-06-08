using System;

namespace SportsData.Core.Dtos.Canonical;

public class FranchiseSeasonRecordStatDto : DtoBase
{
    public Guid FranchiseSeasonRecordId { get; set; }

    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string ShortDisplayName { get; set; }

    public string Description { get; set; }

    public string Abbreviation { get; set; }

    public string Type { get; set; }

    public double Value { get; set; }

    public string DisplayValue { get; set; }
}