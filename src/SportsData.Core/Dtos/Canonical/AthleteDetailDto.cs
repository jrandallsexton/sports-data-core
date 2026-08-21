using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical;

/// <summary>
/// Full athlete drill-down for the web athlete page: the Athlete record,
/// every AthleteSeason (newest first), and each season's statistic
/// documents with their categories and stats. Deliberately verbose — this
/// page exists to spot-check sourced data without opening the database, so
/// provenance fields (doc CreatedUtc, split identifiers) that a normal
/// product surface would omit are part of the contract here: they are what
/// make duplicate docs and stale vintages visible.
/// </summary>
public class AthleteDetailDto
{
    public Guid Id { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ShortName { get; set; }
    public string? Slug { get; set; }
    public string? Jersey { get; set; }
    public string? HeightDisplay { get; set; }
    public string? WeightDisplay { get; set; }
    public DateTime? DoB { get; set; }
    public string? BirthLocation { get; set; }
    public string? ExperienceDisplayValue { get; set; }
    public int ExperienceYears { get; set; }
    public int? DebutYear { get; set; }
    public string? DraftDisplayText { get; set; }
    public bool IsActive { get; set; }
    public string? StatusName { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ModifiedUtc { get; set; }

    public List<AthleteSeasonDetailDto> Seasons { get; set; } = [];
}

public class AthleteSeasonDetailDto
{
    public Guid AthleteSeasonId { get; set; }
    public int? SeasonYear { get; set; }
    public string? TeamName { get; set; }
    public string? TeamSlug { get; set; }
    public string? Position { get; set; }
    public string? PositionAbbreviation { get; set; }
    public string? Jersey { get; set; }
    public string? ExperienceDisplayValue { get; set; }
    public bool IsActive { get; set; }
    public string? StatusName { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ModifiedUtc { get; set; }

    public List<AthleteSeasonStatisticDetailDto> Statistics { get; set; } = [];
}

/// <summary>One sourced statistics DOCUMENT (a season can carry several —
/// splits, and historically duplicates; surfacing them all is the point).</summary>
public class AthleteSeasonStatisticDetailDto
{
    public Guid Id { get; set; }
    public string? SplitId { get; set; }
    public string? SplitName { get; set; }
    public string? SplitType { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ModifiedUtc { get; set; }

    public List<AthleteStatisticCategoryDto> Categories { get; set; } = [];
}

public class AthleteStatisticCategoryDto
{
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Summary { get; set; }

    public List<AthleteStatisticStatDto> Stats { get; set; } = [];
}

public class AthleteStatisticStatDto
{
    public string? DisplayName { get; set; }
    public string? Abbreviation { get; set; }
    public string? DisplayValue { get; set; }
    public string? PerGameDisplayValue { get; set; }
}
