using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionExtensions
    {
        public static FootballCompetition AsFootballEntity(
            this EspnEventCompetitionDtoBase dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid contestId,
            Guid correlationId)
        {
            var entity = new FootballCompetition();
            MapSharedProperties(dto, entity, externalRefIdentityGenerator, contestId, correlationId);

            if (dto is not EspnFootballEventCompetitionDto footballDto)
            {
                throw new InvalidOperationException(
                    $"Expected EspnFootballEventCompetitionDto but got {dto.GetType().Name}");
            }

            entity.DateValid = footballDto.DateValid;
            entity.HasDefensiveStats = footballDto.HasDefensiveStats;

            return entity;
        }

        public static BaseballCompetition AsBaseballEntity(
            this EspnEventCompetitionDtoBase dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid contestId,
            Guid correlationId)
        {
            var entity = new BaseballCompetition();
            MapSharedProperties(dto, entity, externalRefIdentityGenerator, contestId, correlationId);
            return entity;
        }

        private static void MapSharedProperties(
            EspnEventCompetitionDtoBase dto,
            CompetitionBase entity,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid contestId,
            Guid correlationId)
        {
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            entity.Id = identity.CanonicalId;
            entity.Attendance = dto.Attendance;
            entity.ContestId = contestId;
            entity.CreatedBy = correlationId;
            entity.CreatedUtc = DateTime.UtcNow;
            entity.Date = DateTime.TryParse(dto.Date, out var date) ? date.ToUniversalTime() : DateTime.MinValue.ToUniversalTime();
            entity.FormatOvertimeDisplayName = dto.Format?.Overtime?.DisplayName;
            entity.FormatOvertimePeriods = dto.Format?.Overtime?.Periods;
            entity.FormatOvertimeSlug = dto.Format?.Overtime?.Slug;
            entity.FormatRegulationClock = dto.Format?.Regulation?.Clock;
            entity.FormatRegulationDisplayName = dto.Format?.Regulation?.DisplayName;
            entity.FormatRegulationPeriods = dto.Format?.Regulation?.Periods;
            entity.FormatRegulationSlug = dto.Format?.Regulation?.Slug;
            entity.IsBoxscoreAvailable = dto.BoxscoreAvailable;
            entity.IsBracketAvailable = dto.BracketAvailable;
            entity.IsCommentaryAvailable = dto.CommentaryAvailable;
            entity.IsConferenceCompetition = dto.ConferenceCompetition;
            entity.IsConversationAvailable = dto.ConversationAvailable;
            entity.IsDivisionCompetition = dto.DivisionCompetition;
            entity.IsGamecastAvailable = dto.GamecastAvailable;
            entity.IsHighlightsAvailable = dto.HighlightsAvailable;
            entity.IsLineupAvailable = dto.LineupAvailable;
            entity.IsLiveAvailable = dto.LiveAvailable;
            entity.IsNeutralSite = dto.NeutralSite;
            entity.IsOnWatchEspn = dto.OnWatchESPN;
            entity.IsPickCenterAvailable = dto.PickcenterAvailable;
            entity.IsPlayByPlayAvailable = dto.PlayByPlayAvailable;
            entity.IsPossessionArrowAvailable = dto.PossessionArrowAvailable;
            entity.IsPreviewAvailable = dto.PreviewAvailable;
            entity.IsRecapAvailable = dto.RecapAvailable;
            entity.IsRecent = dto.Recent;
            entity.IsShotChartAvailable = dto.ShotChartAvailable;
            entity.IsSummaryAvailable = dto.SummaryAvailable;
            entity.IsTicketsAvailable = dto.TicketsAvailable;
            entity.IsTimeoutsAvailable = dto.TimeoutsAvailable;
            entity.IsWallClockAvailable = dto.WallclockAvailable;
            entity.TimeValid = dto.TimeValid;
            entity.TypeAbbreviation = dto.Type?.Abbreviation;
            entity.TypeId = dto.Type?.Id;
            entity.TypeName = dto.Type?.TypeName;
            entity.TypeSlug = dto.Type?.Slug;
            entity.TypeText = dto.Type?.Text;
            entity.ExternalIds = new List<CompetitionExternalId>()
            {
                new()
                {
                    Id = identity.CanonicalId,
                    Value = identity.UrlHash,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = identity.UrlHash,
                    SourceUrl = identity.CleanUrl
                }
            };
        }
    }
}
