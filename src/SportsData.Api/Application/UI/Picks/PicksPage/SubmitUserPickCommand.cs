namespace SportsData.Api.Application.UI.Picks.PicksPage
{
    public class SubmitUserPickCommand
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
    }
}
