using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical;

public class TeamRosterDto
{
    public List<TeamRosterEntryDto> Players { get; set; } = [];
}

public class TeamRosterEntryDto
{
    public Guid AthleteSeasonId { get; set; }
    public string? DisplayName { get; set; }
    public string? ShortName { get; set; }
    public string? Slug { get; set; }
    public string? Jersey { get; set; }
    public string? Position { get; set; }
    public string? PositionAbbreviation { get; set; }
    public string? HeightDisplay { get; set; }
    public string? WeightDisplay { get; set; }
    public string? ExperienceDisplayValue { get; set; }
    public int ExperienceYears { get; set; }
    public bool IsActive { get; set; }
}
