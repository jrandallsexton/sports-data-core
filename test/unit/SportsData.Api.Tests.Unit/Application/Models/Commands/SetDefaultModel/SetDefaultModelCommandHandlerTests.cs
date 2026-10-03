using Moq;

using SportsData.Api.Application.Models.Commands.SetDefaultModel;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Models.Commands.SetDefaultModel
{
    public class SetDefaultModelCommandHandlerTests : ApiTestBase<SetDefaultModelCommandHandler>
    {
        private static readonly DateTime Now = new(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        public SetDefaultModelCommandHandlerTests()
        {
            Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        }

        private async Task<ModelProvider> SeedProviderAsync(string name = "Anthropic", ModelProviderKind kind = ModelProviderKind.Anthropic)
        {
            var provider = new ModelProvider { Id = Guid.NewGuid(), Name = name, Kind = kind };
            await DataContext.ModelProviders.AddAsync(provider);
            await DataContext.SaveChangesAsync();
            return provider;
        }

        [Fact]
        public async Task SetDefaultModel_FlipsSingleGlobalSlot_AndRejectsInactive()
        {
            var provider = await SeedProviderAsync();
            var current = new Model { Id = Guid.NewGuid(), ModelProviderId = provider.Id, Name = "A", ApiModelId = "a", IsDefault = true };
            var challenger = new Model { Id = Guid.NewGuid(), ModelProviderId = provider.Id, Name = "B", ApiModelId = "b" };
            var inactive = new Model { Id = Guid.NewGuid(), ModelProviderId = provider.Id, Name = "C", ApiModelId = "c", IsActive = false };
            await DataContext.Models.AddRangeAsync(current, challenger, inactive);
            await DataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<SetDefaultModelCommandHandler>();

            // Inactive model cannot become the production default
            var rejected = await sut.ExecuteAsync(inactive.Id, CancellationToken.None);
            Assert.False(rejected.IsSuccess);

            // Promotion flips the single global slot
            var promoted = await sut.ExecuteAsync(challenger.Id, CancellationToken.None);
            Assert.True(promoted.IsSuccess);
            var single = Assert.Single(DataContext.Models.Where(m => m.IsDefault).ToList());
            Assert.Equal(challenger.Id, single.Id);
        }
    }
}
