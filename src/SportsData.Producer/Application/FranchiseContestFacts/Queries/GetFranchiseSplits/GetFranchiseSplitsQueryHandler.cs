using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.FranchiseContestFacts.Queries.GetFranchiseSplits;

public interface IGetFranchiseSplitsQueryHandler
{
    Task<Result<FranchiseSplitsDto>> ExecuteAsync(
        GetFranchiseSplitsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// War Room Splits: a team's record over a filtered set of its games, read
/// from FranchiseContestFact (one row per team per finalized FBS-involved
/// game).
///
/// The filters are a WHERE over the fact rows; the aggregation runs here in
/// C# because a team has at most a few hundred rows across all seasons and
/// the rules (pick'em is neither favorite nor underdog, an unknown rank is
/// neither ranked nor unranked) read more plainly than SQL.
///
/// Each filter has a data floor: the first season its column is trustworthy
/// (the "Data tiers" table in the War Room analytics design). A request that
/// uses a filter starts no earlier than that floor, and the response reports
/// the effective start so the UI can say so instead of implying the earlier
/// seasons had no such games.
/// </summary>
public class GetFranchiseSplitsQueryHandler : IGetFranchiseSplitsQueryHandler
{
    /// <summary>Fact rows are projected from 2001 (2000 kickoffs are date-only and its conference membership is unverified).</summary>
    private const int ResultsFloor = 2001;

    /// <summary>Rank going in (ESPN curatedRank on the competitor).</summary>
    private const int RankFloor = 2001;

    /// <summary>Kickoff time and venue time zone.</summary>
    private const int KickoffWindowFloor = 2001;

    /// <summary>Spreads.</summary>
    private const int LineFloor = 2012;

    /// <summary>
    /// Conference game. ESPN's flag from 2014; before that the projector derives
    /// it from season-accurate conference membership (#857), which holds from 2001.
    /// </summary>
    private const int ConferenceGameFloor = 2001;

    /// <summary>Spread size reads the same spreads as the line filter.</summary>
    private const int SpreadSizeFloor = LineFloor;

    // Opponent, opponent conference, season type and month need no floor of
    // their own: they read columns trustworthy from the results floor (2001).

    private const int RegularSeasonPhaseTypeCode = 2;
    private const int PostseasonPhaseTypeCode = 3;

    private const int Unranked = 99;
    private const int RankedMax = 25;
    private const int Top10Max = 10;

    private readonly FootballDataContext _dataContext;
    private readonly IValidator<GetFranchiseSplitsQuery> _validator;

    public GetFranchiseSplitsQueryHandler(
        FootballDataContext dataContext,
        IValidator<GetFranchiseSplitsQuery> validator)
    {
        _dataContext = dataContext;
        _validator = validator;
    }

    public async Task<Result<FranchiseSplitsDto>> ExecuteAsync(
        GetFranchiseSplitsQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<FranchiseSplitsDto>(
                default!,
                ResultStatus.Validation,
                validation.Errors);
        }

        var franchise = await _dataContext.Franchises
            .AsNoTracking()
            .Where(f => f.Slug == query.Slug)
            .Select(f => new { f.Id, f.Slug, f.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);

        if (franchise is null)
        {
            return new Failure<FranchiseSplitsDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure(nameof(query.Slug), $"Franchise '{query.Slug}' not found")]);
        }

        var seasonFrom = EffectiveSeasonFrom(query);

        var dto = new FranchiseSplitsDto
        {
            FranchiseSlug = franchise.Slug,
            FranchiseName = franchise.DisplayName,
            RequestedSeasonFrom = query.SeasonFrom,
            SeasonFrom = seasonFrom,
            SeasonTo = query.SeasonTo,
            Options = await GetOptionsAsync(franchise.Id, query, cancellationToken),
        };

        if (seasonFrom > query.SeasonTo)
            return new Success<FranchiseSplitsDto>(dto);

        // An unknown opponent slug matches no games rather than failing: a
        // stale or mistyped value in a shared link should not break the page.
        Guid? opponentId = null;
        if (!string.IsNullOrWhiteSpace(query.Opponent))
        {
            opponentId = await _dataContext.Franchises
                .AsNoTracking()
                .Where(f => f.Slug == query.Opponent)
                .Select(f => (Guid?)f.Id)
                .FirstOrDefaultAsync(cancellationToken) ?? Guid.Empty;
        }

        var rows = await ApplyFilters(
                _dataContext.FranchiseContestFacts.AsNoTracking()
                    .Where(f => f.FranchiseId == franchise.Id
                                && f.SeasonYear >= seasonFrom
                                && f.SeasonYear <= query.SeasonTo),
                query,
                opponentId)
            .Join(
                _dataContext.Franchises,
                f => f.OpponentFranchiseId,
                o => o.Id,
                (f, o) => new FactRow(
                    f.ContestId,
                    f.SeasonYear,
                    f.Week,
                    f.SeasonPhaseTypeCode,
                    f.KickoffUtc,
                    f.Site,
                    f.KickoffWindow,
                    f.IsConferenceGame,
                    f.RankGoingIn,
                    o.Slug,
                    o.DisplayName,
                    f.OpponentRankGoingIn,
                    f.PointsFor,
                    f.PointsAgainst,
                    f.Result,
                    f.Spread,
                    f.AtsResult))
            .ToListAsync(cancellationToken);

        dto.Summary = Summarize(rows);
        dto.Games = rows
            .OrderByDescending(r => r.KickoffUtc)
            .Select(ToGame)
            .ToList();

        return new Success<FranchiseSplitsDto>(dto);
    }

    /// <summary>The requested start raised to the floor of every filter in use.</summary>
    private static int EffectiveSeasonFrom(GetFranchiseSplitsQuery query)
    {
        var floor = ResultsFloor;
        if (query.OpponentRank.HasValue) floor = Math.Max(floor, RankFloor);
        if (query.KickoffWindow.HasValue) floor = Math.Max(floor, KickoffWindowFloor);
        if (query.Line.HasValue) floor = Math.Max(floor, LineFloor);
        if (query.ConferenceGame.HasValue) floor = Math.Max(floor, ConferenceGameFloor);
        if (query.SpreadSize.HasValue) floor = Math.Max(floor, SpreadSizeFloor);
        return Math.Max(query.SeasonFrom, floor);
    }

    private static IQueryable<FranchiseContestFact> ApplyFilters(
        IQueryable<FranchiseContestFact> facts,
        GetFranchiseSplitsQuery query,
        Guid? opponentId)
    {
        if (query.Site.HasValue)
            facts = facts.Where(f => f.Site == query.Site.Value);

        facts = query.OpponentRank switch
        {
            OpponentRankFilter.Ranked => facts.Where(f => f.OpponentRankGoingIn >= 1 && f.OpponentRankGoingIn <= RankedMax),
            OpponentRankFilter.Top10 => facts.Where(f => f.OpponentRankGoingIn >= 1 && f.OpponentRankGoingIn <= Top10Max),
            OpponentRankFilter.Unranked => facts.Where(f => f.OpponentRankGoingIn == Unranked),
            _ => facts
        };

        facts = query.Line switch
        {
            LineFilter.Favorite => facts.Where(f => f.Spread < 0),
            LineFilter.Underdog => facts.Where(f => f.Spread > 0),
            _ => facts
        };

        if (query.KickoffWindow.HasValue)
            facts = facts.Where(f => f.KickoffWindow == query.KickoffWindow.Value);

        if (query.ConferenceGame.HasValue)
            facts = facts.Where(f => f.IsConferenceGame == query.ConferenceGame.Value);

        // The opponent's conference in that season, as projected.
        if (!string.IsNullOrWhiteSpace(query.OpponentConference))
            facts = facts.Where(f => f.OpponentConference == query.OpponentConference);

        if (opponentId.HasValue)
            facts = facts.Where(f => f.OpponentFranchiseId == opponentId.Value);

        facts = query.SeasonType switch
        {
            SeasonTypeFilter.Regular => facts.Where(f => f.SeasonPhaseTypeCode == RegularSeasonPhaseTypeCode),
            SeasonTypeFilter.Postseason => facts.Where(f => f.SeasonPhaseTypeCode == PostseasonPhaseTypeCode),
            _ => facts
        };

        // A game without a venue has no local month and matches no month.
        if (query.Month.HasValue)
            facts = facts.Where(f => f.LocalMonth == query.Month.Value);

        // Bands on the absolute line; no line matches none.
        facts = query.SpreadSize switch
        {
            SpreadSizeFilter.Close => facts.Where(f => f.Spread >= -3m && f.Spread <= 3m),
            SpreadSizeFilter.Moderate => facts.Where(f => (f.Spread > 3m && f.Spread < 7m) || (f.Spread < -3m && f.Spread > -7m)),
            SpreadSizeFilter.Large => facts.Where(f => (f.Spread >= 7m && f.Spread < 14m) || (f.Spread <= -7m && f.Spread > -14m)),
            SpreadSizeFilter.Huge => facts.Where(f => f.Spread >= 14m || f.Spread <= -14m),
            _ => facts
        };

        return facts;
    }

    /// <summary>
    /// Picker lists for the opponent and opponent-conference filters: every
    /// opponent and conference in the requested season range, ignoring the
    /// chip filters (and their floors) so a picker never empties itself when
    /// a chip changes.
    /// </summary>
    private async Task<FranchiseSplitsOptionsDto> GetOptionsAsync(
        Guid franchiseId,
        GetFranchiseSplitsQuery query,
        CancellationToken cancellationToken)
    {
        var seasonFrom = Math.Max(query.SeasonFrom, ResultsFloor);
        var facts = _dataContext.FranchiseContestFacts.AsNoTracking()
            .Where(f => f.FranchiseId == franchiseId
                        && f.SeasonYear >= seasonFrom
                        && f.SeasonYear <= query.SeasonTo);

        var opponentCounts = await facts
            .GroupBy(f => f.OpponentFranchiseId)
            .Select(g => new { OpponentFranchiseId = g.Key, Games = g.Count() })
            .ToListAsync(cancellationToken);

        var opponentIds = opponentCounts.Select(o => o.OpponentFranchiseId).ToList();
        var opponentNames = await _dataContext.Franchises
            .AsNoTracking()
            .Where(f => opponentIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Slug, f.DisplayName })
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        var conferenceCounts = await facts
            .Where(f => f.OpponentConference != null && f.OpponentConference != "")
            .GroupBy(f => f.OpponentConference!)
            .Select(g => new { Abbreviation = g.Key, Games = g.Count() })
            .ToListAsync(cancellationToken);

        // Label: the conference's short name in the most recent season that
        // used the abbreviation (e.g. "midam" is "MAC"); the abbreviation
        // itself when no group carries it.
        var abbreviations = conferenceCounts.Select(c => c.Abbreviation).ToList();
        var groups = await _dataContext.GroupSeasons
            .AsNoTracking()
            .Where(g => abbreviations.Contains(g.Abbreviation) && g.ShortName != null)
            .Select(g => new { g.Abbreviation, g.SeasonYear, g.ShortName })
            .ToListAsync(cancellationToken);
        var labels = groups
            .GroupBy(g => g.Abbreviation)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.SeasonYear).First().ShortName!);

        return new FranchiseSplitsOptionsDto
        {
            Opponents = opponentCounts
                .Where(o => opponentNames.ContainsKey(o.OpponentFranchiseId))
                .Select(o => new FranchiseSplitsOpponentOptionDto
                {
                    Slug = opponentNames[o.OpponentFranchiseId].Slug,
                    Name = opponentNames[o.OpponentFranchiseId].DisplayName,
                    Games = o.Games,
                })
                .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Conferences = conferenceCounts
                .Select(c => new FranchiseSplitsConferenceOptionDto
                {
                    Value = c.Abbreviation,
                    Label = labels.GetValueOrDefault(c.Abbreviation, c.Abbreviation),
                    Games = c.Games,
                })
                .OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private static FranchiseSplitsSummaryDto Summarize(IReadOnlyCollection<FactRow> rows)
    {
        var scored = rows
            .Where(r => r.PointsFor.HasValue && r.PointsAgainst.HasValue)
            .ToList();

        return new FranchiseSplitsSummaryDto
        {
            Games = rows.Count,
            Wins = rows.Count(r => r.Result == GameResult.Win),
            Losses = rows.Count(r => r.Result == GameResult.Loss),
            Ties = rows.Count(r => r.Result == GameResult.Tie),
            AtsWins = rows.Count(r => r.AtsResult == AtsResult.Covered),
            AtsLosses = rows.Count(r => r.AtsResult == AtsResult.NotCovered),
            AtsPushes = rows.Count(r => r.AtsResult == AtsResult.Push),
            AvgMargin = Average(scored, r => r.PointsFor!.Value - r.PointsAgainst!.Value),
            AvgPointsFor = Average(scored, r => r.PointsFor!.Value),
            AvgPointsAgainst = Average(scored, r => r.PointsAgainst!.Value),
            FavoriteWins = rows.Count(r => r.Spread < 0 && r.Result == GameResult.Win),
            FavoriteLosses = rows.Count(r => r.Spread < 0 && r.Result == GameResult.Loss),
            UnderdogWins = rows.Count(r => r.Spread > 0 && r.Result == GameResult.Win),
            UnderdogLosses = rows.Count(r => r.Spread > 0 && r.Result == GameResult.Loss),
        };
    }

    private static decimal? Average(IReadOnlyCollection<FactRow> rows, Func<FactRow, int> selector) =>
        rows.Count == 0
            ? null
            : Math.Round((decimal)rows.Sum(selector) / rows.Count, 1, MidpointRounding.AwayFromZero);

    private static FranchiseSplitsGameDto ToGame(FactRow r) => new()
    {
        ContestId = r.ContestId,
        SeasonYear = r.SeasonYear,
        Week = r.Week,
        SeasonPhaseTypeCode = r.SeasonPhaseTypeCode,
        KickoffUtc = DateTime.SpecifyKind(r.KickoffUtc, DateTimeKind.Utc),
        Site = r.Site.ToString(),
        KickoffWindow = r.KickoffWindow?.ToString(),
        IsConferenceGame = r.IsConferenceGame,
        RankGoingIn = r.RankGoingIn,
        OpponentSlug = r.OpponentSlug,
        OpponentName = r.OpponentName,
        OpponentRankGoingIn = r.OpponentRankGoingIn,
        PointsFor = r.PointsFor,
        PointsAgainst = r.PointsAgainst,
        Result = r.Result?.ToString(),
        Spread = r.Spread,
        AtsResult = r.AtsResult?.ToString(),
    };

    private record FactRow(
        Guid ContestId,
        int SeasonYear,
        int? Week,
        int? SeasonPhaseTypeCode,
        DateTime KickoffUtc,
        GameSite Site,
        KickoffWindow? KickoffWindow,
        bool IsConferenceGame,
        int? RankGoingIn,
        string OpponentSlug,
        string OpponentName,
        int? OpponentRankGoingIn,
        int? PointsFor,
        int? PointsAgainst,
        GameResult? Result,
        decimal? Spread,
        AtsResult? AtsResult);
}
