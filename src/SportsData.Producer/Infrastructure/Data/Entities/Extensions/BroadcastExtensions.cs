using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class BroadcastExtensions
{
    public static CompetitionBroadcast AsEntity(
        this EspnEventCompetitionBroadcastItem dto,
        Guid competitionId)
    {
        return new CompetitionBroadcast
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

            MediaId = dto.EspnEventCompetitionBroadcastItemMedia?.Id,
            MediaCallLetters = dto.EspnEventCompetitionBroadcastItemMedia?.CallLetters,
            MediaName = dto.EspnEventCompetitionBroadcastItemMedia?.Name,
            MediaShortName = dto.EspnEventCompetitionBroadcastItemMedia?.ShortName,
            MediaSlug = dto.EspnEventCompetitionBroadcastItemMedia?.Slug,

            MediaGroupId = dto.EspnEventCompetitionBroadcastItemMedia?.Group?.Id,
            MediaGroupName = dto.EspnEventCompetitionBroadcastItemMedia?.Group?.Name,
            MediaGroupSlug = dto.EspnEventCompetitionBroadcastItemMedia?.Group?.Slug,

            Language = dto.Lang,
            Region = dto.Region,

            Partnered = dto.Partnered
        };
    }
}