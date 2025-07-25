using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CompetitionLink : EntityBase<Guid>
{
    public required string Rel { get; set; }

    public required string Href { get; set; }

    public string? Text { get; set; }

    public string? ShortText { get; set; }

    public bool IsExternal { get; set; }

    public bool IsPremium { get; set; }

    public Guid CompetitionId { get; set; }

    public Competition Competition { get; set; } = null!;
}