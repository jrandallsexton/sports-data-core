using System;
using System.Collections.Generic;

namespace SportsData.Api.Application.Admin.Queries.GetModelLabMatrix;

/// <summary>
/// The week matrix: rows = contests (each rendered as an SU line and an
/// ATS line by the UI), columns = models. A missing cell means that
/// (contest, model) pair has no experiment yet — the UI offers to
/// generate it. Consensus is computed client-side from the cells.
/// </summary>
public class ModelLabMatrixDto
{
    public List<MatrixModelDto> Models { get; set; } = [];

    public List<MatrixContestDto> Contests { get; set; } = [];

    public class MatrixModelDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = default!;
    }

    public class MatrixContestDto
    {
        public Guid ContestId { get; set; }

        public DateTime StartDateUtc { get; set; }

        public string Away { get; set; } = default!;

        public string AwayShort { get; set; } = default!;

        public Guid AwayFranchiseSeasonId { get; set; }

        public string Home { get; set; } = default!;

        public string HomeShort { get; set; } = default!;

        public Guid HomeFranchiseSeasonId { get; set; }

        /// <summary>Current line, HOME-relative (negative = home favored, e.g. -22.5); null when no odds. The team name would be redundant — the spread is always the home team's.</summary>
        public double? Spread { get; set; }

        /// <summary>True once the contest is completed — the gate for grading picks. Unfinalized games render ungraded.</summary>
        public bool IsFinal { get; set; }

        /// <summary>Actual straight-up winner (FranchiseSeasonId); null until final (or on a tie).</summary>
        public Guid? ActualWinnerId { get; set; }

        /// <summary>Actual ATS winner (FranchiseSeasonId); null until final — and null on a PUSH, which grades nobody.</summary>
        public Guid? ActualSpreadWinnerId { get; set; }

        /// <summary>Latest experiment per model; a model absent here has no run yet.</summary>
        public List<MatrixCellDto> Cells { get; set; } = [];
    }

    public class MatrixCellDto
    {
        public Guid ModelId { get; set; }

        /// <summary>Parsed SU pick (FranchiseSeasonId); null = abstained/unparsed.</summary>
        public Guid? PredictedStraightUpWinnerId { get; set; }

        /// <summary>Parsed ATS pick (FranchiseSeasonId); null = abstained/unparsed.</summary>
        public Guid? PredictedSpreadWinnerId { get; set; }

        /// <summary>Validation problems recorded on the capture; null = clean.</summary>
        public string? Problems { get; set; }

        public DateTime CreatedUtc { get; set; }
    }
}
