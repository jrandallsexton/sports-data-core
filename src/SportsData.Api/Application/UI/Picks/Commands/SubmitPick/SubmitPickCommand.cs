using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Picks.Commands.SubmitPick;

public class SubmitPickCommand
{
    public Guid UserId { get; set; }

    public Guid PickemGroupId { get; set; }

    public Guid ContestId { get; set; }

    public int Week { get; set; }

    public PickType PickType { get; set; }

    public Guid? FranchiseSeasonId { get; set; }

    public OverUnderPick? OverUnder { get; set; }

    public int? ConfidencePoints { get; set; }

    public int? TiebreakerGuessTotal { get; set; }

    public int? TiebreakerGuessHome { get; set; }

    public int? TiebreakerGuessAway { get; set; }

    /// <summary>
    /// When this pick was created/updated by importing from another league, the
    /// id of the source <see cref="Infrastructure.Data.Entities.PickemGroupUserPick"/>
    /// it was copied from. Null for hand-made picks. Enables import traceability
    /// and idempotent re-runs. See docs/features/pick-import-across-leagues.md.
    /// </summary>
    public Guid? ImportedFromPickId { get; set; }
}
