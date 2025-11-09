namespace SportsData.Api.Application.UI.Picks.Dtos
{
    public record PickRecordWidgetDto
    {
        public int SeasonYear { get; init; }

        public int AsOfWeek { get; init; }

        public List<PickRecordWidgetItem> Items { get; init; } = [];

        public record PickRecordWidgetItem
        {
            public Guid LeagueId { get; init; }

            public required string LeagueName { get; init; }

            public int Correct { get; init; }

            public int Incorrect { get; init; }

            public int? Pushes { get; init; }

            public double Accuracy { get; init; }
        }
    }
}
