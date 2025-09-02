namespace SportsData.Api.Application.UI.Picks.Dtos
{
    public class PickRecordWidgetDto
    {
        public int SeasonYear { get; set; }

        public int AsOfWeek { get; set; }

        public List<PickRecordWidgetItem> Items { get; set; } = [];

        public class PickRecordWidgetItem
        {
            public Guid LeagueId { get; set; }

            public required string LeagueName { get; set; }

            public int Correct { get; set; }

            public int Incorrect { get; set; }

            public int? Pushes { get; set; }

            public double Accuracy { get; set; }
        }
    }
}
