using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class EspnTeamSeasonRecordExtensions
{
    public static FranchiseSeasonRecord AsEntity(
        this EspnTeamSeasonRecordItemDto dto,
        Guid franchiseSeasonId,
        Guid franchiseId,
        int seasonYear,
        Guid createdBy)
    {
        return new FranchiseSeasonRecord
        {
            Id = Guid.NewGuid(),
            FranchiseSeasonId = franchiseSeasonId,
            FranchiseId = franchiseId,
            SeasonYear = seasonYear,
            Name = dto.Name,
            Abbreviation = dto.Abbreviation,
            DisplayName = dto.DisplayName,
            ShortDisplayName = dto.ShortDisplayName,
            Description = dto.Description,
            Type = dto.Type,
            Summary = dto.Summary,
            DisplayValue = dto.DisplayValue,
            Value = dto.Value,
            CreatedBy = createdBy,
            Stats = dto.Stats?.Select(s => s.AsEntity()).ToList() ?? new()
        };
    }

    public static FranchiseSeasonRecordStat AsEntity(this EspnTeamSeasonRecordItemStatDto dto)
    {
        return new FranchiseSeasonRecordStat
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            ShortDisplayName = dto.ShortDisplayName,
            Description = dto.Description,
            Abbreviation = dto.Abbreviation,
            Type = dto.Type,
            Value = dto.Value,
            DisplayValue = dto.DisplayValue
        };
    }

    public static FranchiseSeasonRecordDto AsCanonical(this FranchiseSeasonRecord entity)
    {
        return new FranchiseSeasonRecordDto
        {
            Id = entity.Id,
            CreatedUtc = entity.CreatedUtc,
            UpdatedUtc = entity.ModifiedUtc,

            FranchiseSeasonId = entity.FranchiseSeasonId,
            FranchiseId = entity.FranchiseId,
            SeasonYear = entity.SeasonYear,

            Name = entity.Name,
            Abbreviation = entity.Abbreviation,
            DisplayName = entity.DisplayName,
            ShortDisplayName = entity.ShortDisplayName,
            Description = entity.Description,
            Type = entity.Type,
            Summary = entity.Summary,
            DisplayValue = entity.DisplayValue,
            Value = entity.Value,

            Stats = entity.Stats?.Select(s => new FranchiseSeasonRecordStatDto
            {
                Id = s.Id,
                CreatedUtc = entity.CreatedUtc, // These are likely shared with parent
                UpdatedUtc = entity.ModifiedUtc,

                FranchiseSeasonRecordId = s.FranchiseSeasonRecordId,
                Name = s.Name,
                DisplayName = s.DisplayName,
                ShortDisplayName = s.ShortDisplayName,
                Description = s.Description,
                Abbreviation = s.Abbreviation,
                Type = s.Type,
                Value = s.Value,
                DisplayValue = s.DisplayValue
            }).ToList() ?? new()
        };
    }
}