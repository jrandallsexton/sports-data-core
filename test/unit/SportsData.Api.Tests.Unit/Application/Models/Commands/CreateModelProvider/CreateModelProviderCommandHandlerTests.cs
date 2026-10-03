using Moq;

using SportsData.Api.Application.Models.Commands.CreateModelProvider;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Models.Commands.CreateModelProvider
{
    public class CreateModelProviderCommandHandlerTests : ApiTestBase<CreateModelProviderCommandHandler>
    {
        private static readonly DateTime Now = new(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        public CreateModelProviderCommandHandlerTests()
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
        public async Task CreateProvider_RejectsDuplicateName()
        {
            await SeedProviderAsync("Anthropic");
            var sut = Mocker.CreateInstance<CreateModelProviderCommandHandler>();

            var result = await sut.ExecuteAsync(new CreateModelProviderCommand
            {
                Name = "Anthropic",
                Kind = ModelProviderKind.Anthropic
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Single(DataContext.ModelProviders);
        }
    }
}
