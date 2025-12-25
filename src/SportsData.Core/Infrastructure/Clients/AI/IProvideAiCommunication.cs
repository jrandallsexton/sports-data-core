using SportsData.Core.Common;

using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.AI;

public interface IProvideAiCommunication
{
    Task<Result<string>> GetResponseAsync(
        string prompt,
        CancellationToken ct = default);

    Task<T?> GetTypedResponseAsync<T>(
        string prompt,
        CancellationToken ct = default);

    string GetModelName();
}