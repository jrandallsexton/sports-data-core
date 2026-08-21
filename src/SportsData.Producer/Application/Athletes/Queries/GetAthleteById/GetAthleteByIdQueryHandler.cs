using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteById;

public interface IGetAthleteByIdQueryHandler
{
    Task<Result<AthleteDetailDto>> ExecuteAsync(
        GetAthleteByIdQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Full athlete drill-down: the Athlete record, every AthleteSeason
/// (newest first), and each season's statistic documents down to the stat
/// rows. Built as three sequential projected queries (athlete, seasons,
/// statistics) rather than one Include chain — the stat graph is three
/// levels deep and a single query would explode into a cartesian join.
/// Provenance fields (doc CreatedUtc, split identifiers) are part of the
/// contract: the page this serves exists to spot-check sourced data, so
/// duplicates and stale vintages must be visible, not smoothed over.
/// </summary>
public class GetAthleteByIdQueryHandler : IGetAthleteByIdQueryHandler
{
    private readonly ILogger<GetAthleteByIdQueryHandler> _logger;
    private readonly TeamSportDataContext _dataContext;

    public GetAthleteByIdQueryHandler(
        ILogger<GetAthleteByIdQueryHandler> logger,
        TeamSportDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task<Result<AthleteDetailDto>> ExecuteAsync(
        GetAthleteByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var athleteRow = await _dataContext.Athletes
            .AsNoTracking()
            .Where(a => a.Id == query.AthleteId)
            .Select(a => new
            {
                a.Id,
                a.DisplayName,
                a.FirstName,
                a.LastName,
                a.ShortName,
                a.Slug,
                a.Jersey,
                a.HeightDisplay,
                a.WeightDisplay,
                a.DoB,
                BirthCity = a.BirthLocation == null ? null : a.BirthLocation.City,
                BirthState = a.BirthLocation == null ? null : a.BirthLocation.State,
                BirthCountry = a.BirthLocation == null ? null : a.BirthLocation.Country,
                a.ExperienceDisplayValue,
                a.ExperienceYears,
                a.DebutYear,
                a.DraftDisplayText,
                a.IsActive,
                StatusName = a.Status == null ? null : a.Status.Name,
                a.CreatedUtc,
                a.ModifiedUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (athleteRow is null)
        {
            return new Failure<AthleteDetailDto>(
                default!,
                ResultStatus.NotFound,
                []);
        }

        var birthParts = new[] { athleteRow.BirthCity, athleteRow.BirthState, athleteRow.BirthCountry }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        var athlete = new AthleteDetailDto
        {
            Id = athleteRow.Id,
            DisplayName = athleteRow.DisplayName,
            FirstName = athleteRow.FirstName,
            LastName = athleteRow.LastName,
            ShortName = athleteRow.ShortName,
            Slug = athleteRow.Slug,
            Jersey = athleteRow.Jersey,
            HeightDisplay = athleteRow.HeightDisplay,
            WeightDisplay = athleteRow.WeightDisplay,
            DoB = athleteRow.DoB,
            BirthLocation = birthParts.Length == 0 ? null : string.Join(", ", birthParts),
            ExperienceDisplayValue = athleteRow.ExperienceDisplayValue,
            ExperienceYears = athleteRow.ExperienceYears,
            DebutYear = athleteRow.DebutYear,
            DraftDisplayText = athleteRow.DraftDisplayText,
            IsActive = athleteRow.IsActive,
            StatusName = athleteRow.StatusName,
            CreatedUtc = athleteRow.CreatedUtc,
            ModifiedUtc = athleteRow.ModifiedUtc
        };

        var seasons = await _dataContext.AthleteSeasons
            .AsNoTracking()
            .Where(s => s.AthleteId == query.AthleteId)
            .Select(s => new
            {
                Dto = new AthleteSeasonDetailDto
                {
                    AthleteSeasonId = s.Id,
                    Position = s.Position.Name,
                    PositionAbbreviation = s.Position.Abbreviation,
                    Jersey = s.Jersey,
                    ExperienceDisplayValue = s.ExperienceDisplayValue,
                    IsActive = s.IsActive,
                    StatusName = s.Status == null ? null : s.Status.Name,
                    CreatedUtc = s.CreatedUtc,
                    ModifiedUtc = s.ModifiedUtc
                },
                s.FranchiseSeasonId
            })
            .ToListAsync(cancellationToken);

        // FranchiseSeason has no navigation from AthleteSeason — resolve the
        // team identity (name/slug/year) with a keyed lookup instead.
        var franchiseSeasonIds = seasons
            .Where(s => s.FranchiseSeasonId.HasValue)
            .Select(s => s.FranchiseSeasonId!.Value)
            .Distinct()
            .ToList();

        var teamsById = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Where(fs => franchiseSeasonIds.Contains(fs.Id))
            .Select(fs => new { fs.Id, fs.SeasonYear, fs.DisplayName, fs.Slug })
            .ToDictionaryAsync(fs => fs.Id, cancellationToken);

        var seasonIds = seasons.Select(s => s.Dto.AthleteSeasonId).ToList();

        var statistics = await _dataContext.AthleteSeasonStatistics
            .AsNoTracking()
            // Two nested collection projections (Categories -> Stats): the
            // context throws on MultipleCollectionIncludeWarning, and split
            // queries avoid the row-explosion join anyway.
            .AsSplitQuery()
            .Where(st => seasonIds.Contains(st.AthleteSeasonId))
            .Select(st => new
            {
                st.AthleteSeasonId,
                Dto = new AthleteSeasonStatisticDetailDto
                {
                    Id = st.Id,
                    SplitId = st.SplitId,
                    SplitName = st.SplitName,
                    SplitType = st.SplitType,
                    CreatedUtc = st.CreatedUtc,
                    ModifiedUtc = st.ModifiedUtc,
                    Categories = st.Categories
                        .OrderBy(c => c.DisplayName)
                        .Select(c => new AthleteStatisticCategoryDto
                        {
                            Name = c.Name,
                            DisplayName = c.DisplayName,
                            Summary = c.Summary,
                            Stats = c.Stats
                                .OrderBy(x => x.DisplayName)
                                .Select(x => new AthleteStatisticStatDto
                                {
                                    DisplayName = x.DisplayName,
                                    Abbreviation = x.Abbreviation,
                                    DisplayValue = x.DisplayValue,
                                    PerGameDisplayValue = x.PerGameDisplayValue
                                })
                                .ToList()
                        })
                        .ToList()
                }
            })
            .ToListAsync(cancellationToken);

        var statsBySeason = statistics
            .GroupBy(s => s.AthleteSeasonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Dto).ToList());

        foreach (var season in seasons)
        {
            if (season.FranchiseSeasonId.HasValue &&
                teamsById.TryGetValue(season.FranchiseSeasonId.Value, out var team))
            {
                season.Dto.SeasonYear = team.SeasonYear;
                season.Dto.TeamName = team.DisplayName;
                season.Dto.TeamSlug = team.Slug;
            }

            if (statsBySeason.TryGetValue(season.Dto.AthleteSeasonId, out var docs))
            {
                // Newest doc first — vintage ordering is the spot-check signal.
                season.Dto.Statistics = docs
                    .OrderByDescending(d => d.CreatedUtc)
                    .ToList();
            }
        }

        athlete.Seasons = seasons
            .Select(s => s.Dto)
            .OrderByDescending(s => s.SeasonYear ?? 0)
            .ToList();

        return new Success<AthleteDetailDto>(athlete);
    }
}
