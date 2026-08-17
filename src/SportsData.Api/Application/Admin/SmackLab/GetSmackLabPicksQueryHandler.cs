using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;

namespace SportsData.Api.Application.Admin.SmackLab;

/// <summary>
/// A scored pick prepared for the Lab: the preview fact payload (what
/// Notification's resolver and formatter consume) plus display context for
/// the operator's table row.
/// </summary>
public record SmackLabPickDto(
    SmackPreviewPickDto Facts,
    string PickerName,
    string MatchupLabel,
    string PickLabel,
    bool? IsCorrect);

public interface IGetSmackLabPicksQueryHandler
{
    Task<Result<List<SmackLabPickDto>>> ExecuteAsync(Guid leagueId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Composes preview fact payloads for every scored pick in a league.
///
/// <para>
/// FIDELITY: the facts must match what a live send saw, so the derivation of
/// PickedIsHome / PickedSpread / MarketSpread mirrors
/// <c>PickScoringProcessor</c> exactly — FranchiseSeasonId compared against
/// the matchup result's per-side ids; PickedSpread gated to ATS leagues with
/// a non-zero line; MarketSpread ungated. Contest facts come from the same
/// <c>GetMatchupResult</c> call scoring used, fetched once per distinct
/// contest rather than per pick.
/// </para>
/// </summary>
public class GetSmackLabPicksQueryHandler : IGetSmackLabPicksQueryHandler
{
    private readonly AppDataContext _db;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly ILogger<GetSmackLabPicksQueryHandler> _logger;

    public GetSmackLabPicksQueryHandler(
        AppDataContext db,
        IContestClientFactory contestClientFactory,
        ILogger<GetSmackLabPicksQueryHandler> logger)
    {
        _db = db;
        _contestClientFactory = contestClientFactory;
        _logger = logger;
    }

    public async Task<Result<List<SmackLabPickDto>>> ExecuteAsync(
        Guid leagueId,
        CancellationToken cancellationToken = default)
    {
        var league = await _db.PickemGroups
            .AsNoTracking()
            .Where(g => g.Id == leagueId)
            .Select(g => new { g.Id, g.Name, g.Sport, g.PickType })
            .FirstOrDefaultAsync(cancellationToken);

        if (league is null)
        {
            return new Failure<List<SmackLabPickDto>>(
                [], ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure(nameof(leagueId), "League not found.")]);
        }

        var picks = await _db.UserPicks
            .AsNoTracking()
            .Where(p => p.PickemGroupId == leagueId && p.IsCorrect != null)
            .Select(p => new
            {
                p.Id,
                p.UserId,
                p.ContestId,
                p.FranchiseSeasonId,
                p.IsCorrect
            })
            .ToListAsync(cancellationToken);

        if (picks.Count == 0)
            return new Success<List<SmackLabPickDto>>([]);

        var userNames = await _db.Users
            .AsNoTracking()
            .Where(u => picks.Select(p => p.UserId).Distinct().Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Username })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        // One matchup-result fetch per distinct contest — the same call
        // scoring ran on, so scores/spread/abbreviations are the values the
        // live send composed copy from. A contest whose result fetch fails is
        // skipped (logged) rather than failing the whole league.
        var contestClient = _contestClientFactory.Resolve(league.Sport);
        var results = new Dictionary<Guid, Core.Dtos.Canonical.MatchupResult>();
        foreach (var contestId in picks.Select(p => p.ContestId).Distinct())
        {
            var response = await contestClient.GetMatchupResult(contestId, cancellationToken);
            if (response.IsSuccess && response.Value.FinalizedUtc is not null)
            {
                results[contestId] = response.Value;
            }
            else
            {
                _logger.LogWarning(
                    "SmackLab: no finalized matchup result for ContestId {ContestId}; its picks are skipped.",
                    contestId);
            }
        }

        var dtos = new List<SmackLabPickDto>(picks.Count);
        foreach (var pick in picks)
        {
            if (!results.TryGetValue(pick.ContestId, out var result))
                continue;

            // ── Derivation parity with PickScoringProcessor ──────────────
            bool? pickedIsHome = pick.FranchiseSeasonId.HasValue
                ? pick.FranchiseSeasonId == result.HomeFranchiseSeasonId ? true
                  : pick.FranchiseSeasonId == result.AwayFranchiseSeasonId ? false
                  : (bool?)null
                : null;

            double? pickedSpread = null;
            if (pickedIsHome.HasValue
                && league.PickType == PickType.AgainstTheSpread
                && result.Spread is not null && result.Spread.Value != 0)
            {
                pickedSpread = pickedIsHome.Value ? result.Spread.Value : -result.Spread.Value;
            }

            double? marketSpread = null;
            if (pickedIsHome.HasValue
                && result.Spread is not null && result.Spread.Value != 0)
            {
                marketSpread = pickedIsHome.Value ? result.Spread.Value : -result.Spread.Value;
            }
            // ─────────────────────────────────────────────────────────────

            var facts = new SmackPreviewPickDto
            {
                PickId = pick.Id,
                ContestId = pick.ContestId,
                LeagueId = league.Id,
                UserId = pick.UserId,
                AwayAbbreviation = result.AwayAbbreviation,
                HomeAbbreviation = result.HomeAbbreviation,
                AwayScore = result.AwayScore,
                HomeScore = result.HomeScore,
                IsCorrect = pick.IsCorrect,
                PickedIsHome = pickedIsHome,
                PickedSpread = pickedSpread,
                MarketSpread = marketSpread,
                LeagueName = league.Name,
                Sport = league.Sport
            };

            var pickerName = userNames.TryGetValue(pick.UserId, out var user)
                ? user.DisplayName ?? user.Username
                : "(unknown)";

            var pickLabel = pickedIsHome switch
            {
                true => result.HomeAbbreviation ?? "home",
                false => result.AwayAbbreviation ?? "away",
                null => "O/U"
            };

            dtos.Add(new SmackLabPickDto(
                facts,
                pickerName,
                $"{result.AwayAbbreviation ?? "AWY"} {result.AwayScore} @ {result.HomeAbbreviation ?? "HOM"} {result.HomeScore}",
                pickLabel,
                pick.IsCorrect));
        }

        return new Success<List<SmackLabPickDto>>(dtos);
    }
}
