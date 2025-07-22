namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class OutboxPing
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
    }

}
