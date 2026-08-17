using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.SmackLab;

/// <summary>
/// A league eligible for the SmackBot Lab: it has at least one scored pick to
/// preview. ScoredPickCount sizes the work before the operator selects it.
/// </summary>
public record SmackLabLeagueDto(
    Guid LeagueId,
    string Name,
    string Sport,
    string PickType,
    int ScoredPickCount);

public interface IGetSmackLabLeaguesQueryHandler
{
    Task<Result<List<SmackLabLeagueDto>>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Leagues with scored picks, most recently active first. Scored means
/// <c>IsCorrect</c> is populated — the same marker PickScoringProcessor
/// writes; unscored picks have nothing to preview.
/// </summary>
public class GetSmackLabLeaguesQueryHandler : IGetSmackLabLeaguesQueryHandler
{
    private readonly AppDataContext _db;

    public GetSmackLabLeaguesQueryHandler(AppDataContext db)
    {
        _db = db;
    }

    public async Task<Result<List<SmackLabLeagueDto>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Anonymous projection end-to-end on the server: EF cannot translate
        // an OrderBy over a constructor-projected record's property (nor the
        // enum ToStrings), so the DTO is shaped in memory AFTER the ordered
        // rows come back.
        var rows = await _db.UserPicks
            .AsNoTracking()
            .Where(p => p.IsCorrect != null)
            .GroupBy(p => p.PickemGroupId)
            .Select(g => new { LeagueId = g.Key, ScoredPickCount = g.Count() })
            .Join(
                _db.PickemGroups.AsNoTracking(),
                x => x.LeagueId,
                grp => grp.Id,
                (x, grp) => new
                {
                    grp.Id,
                    grp.Name,
                    grp.Sport,
                    grp.PickType,
                    x.ScoredPickCount
                })
            .OrderByDescending(x => x.ScoredPickCount)
            .ToListAsync(cancellationToken);

        var leagues = rows
            .Select(x => new SmackLabLeagueDto(
                x.Id, x.Name, x.Sport.ToString(), x.PickType.ToString(), x.ScoredPickCount))
            .ToList();

        return new Success<List<SmackLabLeagueDto>>(leagues);
    }
}
