using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionExtensions
    {
        public static Competition AsEntity(
            this EspnEventCompetitionDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid contestId,
            Guid correlationId)
        {
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            var entity = new Competition
            {
                Id = identity.CanonicalId,
                Attendance = dto.Attendance,
                IsBracketAvailable = dto.BracketAvailable,
                IsCommentaryAvailable = dto.CommentaryAvailable,
                ContestId = contestId,
                IsConversationAvailable = dto.ConversationAvailable,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                Date = DateTime.TryParse(dto.Date, out var date) ? date.ToUniversalTime() : DateTime.MinValue.ToUniversalTime(),
                DateValid = dto.DateValid,
                FormatOvertimeDisplayName = dto.Format?.Overtime?.DisplayName,
                FormatOvertimePeriods = dto.Format?.Overtime?.Periods,
                FormatOvertimeSlug = dto.Format?.Overtime?.Slug,
                FormatRegulationClock = dto.Format?.Regulation?.Clock,
                FormatRegulationDisplayName = dto.Format?.Regulation?.DisplayName,
                FormatRegulationPeriods = dto.Format?.Regulation?.Periods,
                FormatRegulationSlug = dto.Format?.Regulation?.Slug,
                HasDefensiveStats = dto.HasDefensiveStats,
                IsHighlightsAvailable = dto.HighlightsAvailable,
                IsBoxscoreAvailable = dto.BoxscoreAvailable,
                IsConferenceCompetition = dto.ConferenceCompetition,
                IsDivisionCompetition = dto.DivisionCompetition,
                IsGamecastAvailable = dto.GamecastAvailable,
                IsLineupAvailable = dto.LineupAvailable,
                IsNeutralSite = dto.NeutralSite,
                IsPlayByPlayAvailable = dto.PlayByPlayAvailable,
                IsPreviewAvailable = dto.PreviewAvailable,
                IsRecapAvailable = dto.RecapAvailable,
                IsLiveAvailable = dto.LiveAvailable,
                IsOnWatchEspn = dto.OnWatchESPN,
                IsPickCenterAvailable = dto.PickcenterAvailable,
                IsPossessionArrowAvailable = dto.PossessionArrowAvailable,
                IsRecent = dto.Recent,
                IsShotChartAvailable = dto.ShotChartAvailable,
                IsSummaryAvailable = dto.SummaryAvailable,
                IsTicketsAvailable = dto.TicketsAvailable,
                IsTimeoutsAvailable = dto.TimeoutsAvailable,
                TimeValid = dto.TimeValid,
                TypeAbbreviation = dto.Type?.Abbreviation,
                TypeId = dto.Type?.Id,
                TypeName = dto.Type?.TypeName,
                TypeSlug = dto.Type?.Slug,
                TypeText = dto.Type?.Text,
                IsWallClockAvailable = dto.WallclockAvailable,
            };

            return entity;
        }
    }
}
