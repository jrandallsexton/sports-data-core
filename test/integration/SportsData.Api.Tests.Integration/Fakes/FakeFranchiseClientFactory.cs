using Moq;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Tests.Integration.Fakes;

/// <summary>
/// Swaps the real Producer-bound franchise client for a Moq-backed fake. Tests
/// configure <see cref="Client"/> to canned responses (e.g. slug → Guid mappings
/// for <c>GetConferenceIdsBySlugs</c>).
/// </summary>
public sealed class FakeFranchiseClientFactory : IFranchiseClientFactory
{
    public Mock<IProvideFranchises> Client { get; } = new(MockBehavior.Loose);

    public IProvideFranchises Resolve(Sport mode) => Client.Object;

    /// <summary>
    /// Convenience for the common case: ack every slug back as a fresh Guid.
    /// </summary>
    public void ResolveSlugsAsNewGuids()
    {
        Client
            .Setup(x => x.GetConferenceIdsBySlugs(
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, List<string> slugs, CancellationToken _) =>
                slugs.ToDictionary(_ => Guid.NewGuid(), s => s));
    }
}
