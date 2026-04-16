using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical;

public record FranchiseLogosDto
{
    public Guid FranchiseId { get; init; }
    public string FranchiseName { get; init; } = string.Empty;
    public List<LogoItemDto> FranchiseLogos { get; init; } = [];
    public List<SeasonLogosDto> SeasonLogos { get; init; } = [];
}

public record SeasonLogosDto
{
    public Guid FranchiseSeasonId { get; init; }
    public int SeasonYear { get; init; }
    public List<LogoItemDto> Logos { get; init; } = [];
}

public record LogoItemDto
{
    public Guid Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public long? Width { get; init; }
    public long? Height { get; init; }
    public List<string>? Rel { get; init; }
    public bool? IsForDarkBg { get; init; }
}
