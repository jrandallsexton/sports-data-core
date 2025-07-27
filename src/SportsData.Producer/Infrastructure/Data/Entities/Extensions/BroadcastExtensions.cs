using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class BroadcastExtensions
{
    public static Broadcast AsEntity(
        this EspnEventCompetitionBroadcastItem dto,
        Guid competitionId)
    {
        return new Broadcast
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,

            TypeId = dto.Type.Id,
            TypeShortName = dto.Type.ShortName,
            TypeLongName = dto.Type.LongName,
            TypeSlug = dto.Type.Slug,

            Channel = dto.Channel,
            Station = dto.Station,
            StationKey = dto.StationKey,
            Url = dto.Url,
            Slug = dto.Slug,
            Priority = dto.Priority,

            MarketId = dto.Market?.Id,
            MarketType = dto.Market?.Type,

            MediaId = dto.Media?.Id,
            MediaCallLetters = dto.Media?.CallLetters,
            MediaName = dto.Media?.Name,
            MediaShortName = dto.Media?.ShortName,
            MediaSlug = dto.Media?.Slug,

            MediaGroupId = dto.Media?.Group?.Id,
            MediaGroupName = dto.Media?.Group?.Name,
            MediaGroupSlug = dto.Media?.Group?.Slug,

            Language = dto.Lang,
            Region = dto.Region,

            Partnered = dto.Partnered
        };
    }
}