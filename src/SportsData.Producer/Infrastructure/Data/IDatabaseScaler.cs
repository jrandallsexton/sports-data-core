namespace SportsData.Producer.Infrastructure.Data
{
    public interface IDatabaseScaler
    {
        Task ScaleUpAsync(string reason, CancellationToken cancellationToken = default);
        Task ScaleDownAsync(string reason, CancellationToken cancellationToken = default);
    }

}
