using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    /// <summary>
    /// Display payload for the live at-bat header — names, position
    /// abbreviations, headshot URLs for the current batter and pitcher.
    /// Hydrated once on the publish path so SignalR consumers don't have
    /// to round-trip back to the API for athlete data per play.
    /// </summary>
    public sealed record BaseballAtBatDisplayPayload(
        string? AtBatShortName,
        string? AtBatPositionAbbreviation,
        string? AtBatHeadshotUrl,
        string? PitchingShortName,
        string? PitchingPositionAbbreviation,
        string? PitchingHeadshotUrl)
    {
        public static BaseballAtBatDisplayPayload Empty { get; } =
            new(null, null, null, null, null, null);
    }

    /// <summary>
    /// Shared hydration for the at-bat / pitcher display fields on
    /// <see cref="SportsData.Core.Eventing.Events.Contests.Baseball.BaseballPlayCompleted"/>.
    /// Reused by the live publish path
    /// (<c>BaseballEventCompetitionPlayDocumentProcessor</c>) and by
    /// <c>BaseballContestReplayService</c> so both paths emit the same
    /// wire shape.
    ///
    /// Mirrors the headshot pattern used by GetProbablePitchersAsync:
    /// AthleteSeason.Athlete.Images ordered with sportdeets-mark Rel
    /// preferred, CreatedUtc as the tiebreaker, first wins.
    /// Position abbreviation comes off the participant rows persisted in
    /// PR #310. ShortName falls back to DisplayName when null (defensive
    /// — every observed AthleteSeason has a ShortName today).
    /// </summary>
    public static class BaseballPlayCompletedPayloadBuilder
    {
        private sealed record AthleteSeasonDisplayRow(
            Guid Id,
            string? ShortName,
            string? DisplayName,
            string? HeadshotUrl);

        public static async Task<BaseballAtBatDisplayPayload> HydrateAsync(
            TeamSportDataContext dataContext,
            BaseballCompetitionPlay play,
            CancellationToken cancellationToken = default)
        {
            if (play.AtBatAthleteSeasonId is null && play.PitchingAthleteSeasonId is null)
            {
                return BaseballAtBatDisplayPayload.Empty;
            }

            // PositionId for each side comes off the in-memory participants
            // collection on the play — the publish path runs BEFORE
            // SaveChangesAsync, so the participants table doesn't have
            // these rows yet. Walking the attached collection works for
            // both the create path (new entity, participants added to nav)
            // and the update path (participants re-added in
            // ApplyUpdateAsync). The Type field on the participant maps
            // each row to "pitcher" / "batter".
            Guid? batterPositionId = null;
            Guid? pitcherPositionId = null;
            foreach (var p in play.Participants)
            {
                if (p.PositionId is null) continue;
                if (string.Equals(p.Type, "batter", StringComparison.OrdinalIgnoreCase))
                    batterPositionId ??= p.PositionId;
                else if (string.Equals(p.Type, "pitcher", StringComparison.OrdinalIgnoreCase))
                    pitcherPositionId ??= p.PositionId;
            }

            // Single batched read for the AthleteSeason display shape.
            // Headshot uses the same AthleteSeason → Athlete.Images path
            // as the probable-pitcher feature.
            var seasonIds = new List<Guid>(2);
            if (play.AtBatAthleteSeasonId.HasValue) seasonIds.Add(play.AtBatAthleteSeasonId.Value);
            if (play.PitchingAthleteSeasonId.HasValue) seasonIds.Add(play.PitchingAthleteSeasonId.Value);

            var seasonRows = await dataContext.AthleteSeasons
                .AsNoTracking()
                .Where(s => seasonIds.Contains(s.Id))
                .Select(s => new AthleteSeasonDisplayRow(
                    s.Id,
                    s.ShortName,
                    s.DisplayName,
                    // Headshot priority: sportdeets-generated avatars
                    // (Rel = ["sportdeets-mark"]) win over any ESPN-sourced
                    // images. CreatedUtc breaks ties — preserves prior
                    // behavior for athletes that don't yet have a generated
                    // avatar.
                    s.Athlete != null && s.Athlete.Images.Any()
                        ? s.Athlete.Images
                            .OrderBy(i => i.Rel != null && i.Rel.Contains("sportdeets-mark") ? 0 : 1)
                            .ThenBy(i => i.CreatedUtc)
                            .First().Uri.ToString()
                        : null))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            // Batched read for the position abbreviations referenced by
            // the participants — AthletePosition is a small, stable lookup.
            var positionIds = new List<Guid>(2);
            if (batterPositionId.HasValue) positionIds.Add(batterPositionId.Value);
            if (pitcherPositionId.HasValue) positionIds.Add(pitcherPositionId.Value);

            var positionAbbrevs = positionIds.Count == 0
                ? new Dictionary<Guid, string?>()
                : await dataContext.AthletePositions
                    .AsNoTracking()
                    .Where(p => positionIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => (string?)p.Abbreviation, cancellationToken);

            return new BaseballAtBatDisplayPayload(
                AtBatShortName: ResolveName(seasonRows, play.AtBatAthleteSeasonId),
                AtBatPositionAbbreviation: ResolveAbbrev(positionAbbrevs, batterPositionId),
                AtBatHeadshotUrl: ResolveHeadshot(seasonRows, play.AtBatAthleteSeasonId),
                PitchingShortName: ResolveName(seasonRows, play.PitchingAthleteSeasonId),
                PitchingPositionAbbreviation: ResolveAbbrev(positionAbbrevs, pitcherPositionId),
                PitchingHeadshotUrl: ResolveHeadshot(seasonRows, play.PitchingAthleteSeasonId));
        }

        private static string? ResolveAbbrev(
            IReadOnlyDictionary<Guid, string?> abbrevs, Guid? id)
        {
            if (id is null) return null;
            return abbrevs.TryGetValue(id.Value, out var abbrev) ? abbrev : null;
        }

        private static string? ResolveName(
            IReadOnlyDictionary<Guid, AthleteSeasonDisplayRow> rows, Guid? id)
        {
            if (id is null || !rows.TryGetValue(id.Value, out var row)) return null;
            return row.ShortName ?? row.DisplayName;
        }

        private static string? ResolveHeadshot(
            IReadOnlyDictionary<Guid, AthleteSeasonDisplayRow> rows, Guid? id)
        {
            if (id is null || !rows.TryGetValue(id.Value, out var row)) return null;
            return row.HeadshotUrl;
        }
    }
}
