namespace SportsData.Producer.Infrastructure.Data.Entities;

public class OutboxPing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime PingedUtc { get; set; } = DateTime.UtcNow;
}