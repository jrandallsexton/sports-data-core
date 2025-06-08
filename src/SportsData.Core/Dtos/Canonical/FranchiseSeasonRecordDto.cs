using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical;

public class FranchiseSeasonRecordDto : DtoBase
{
    public Guid FranchiseSeasonId { get; set; }

    public Guid FranchiseId { get; set; }

    public int SeasonYear { get; set; }

    public string RecordId { get; set; }

    public string Name { get; set; }

    public string Abbreviation { get; set; }

    public string DisplayName { get; set; }

    public string ShortDisplayName { get; set; }

    public string Description { get; set; }

    public string Type { get; set; }

    public string Summary { get; set; }

    public string DisplayValue { get; set; }

    public double Value { get; set; }

    public List<FranchiseSeasonRecordStatDto> Stats { get; set; } = [];
}