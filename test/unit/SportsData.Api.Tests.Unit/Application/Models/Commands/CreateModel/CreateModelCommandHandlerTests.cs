using Moq;

using SportsData.Api.Application.Models.Commands.CreateModel;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Models.Commands.CreateModel
{
    public class CreateModelCommandHandlerTests : ApiTestBase<CreateModelCommandHandler>
    {
        private static readonly DateTime Now = new(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        public CreateModelCommandHandlerTests()
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
        public async Task CreateModel_RequiresExistingProvider()
        {
            var sut = Mocker.CreateInstance<CreateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new CreateModelCommand
            {
                ModelProviderId = Guid.NewGuid(),
                Name = "Claude Haiku 4.5",
                ApiModelId = "claude-haiku-4-5"
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(DataContext.Models);
        }

        [Fact]
        public async Task CreateModel_WithDefault_FlipsPreviousDefault()
        {
            var provider = await SeedProviderAsync();
            var incumbent = new Model
            {
                Id = Guid.NewGuid(),
                ModelProviderId = provider.Id,
                Name = "DeepSeek V3",
                ApiModelId = "deepseek-chat",
                IsDefault = true
            };
            await DataContext.Models.AddAsync(incumbent);
            await DataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<CreateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new CreateModelCommand
            {
                ModelProviderId = provider.Id,
                Name = "Claude Haiku 4.5",
                ApiModelId = "claude-haiku-4-5",
                KnowledgeCutoffUtc = new DateTime(2025, 7, 31, 0, 0, 0, DateTimeKind.Utc),
                IsDefault = true
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var defaults = DataContext.Models.Where(m => m.IsDefault).ToList();
            var single = Assert.Single(defaults);
            Assert.Equal("Claude Haiku 4.5", single.Name);
            Assert.False(DataContext.Models.Single(m => m.Id == incumbent.Id).IsDefault);
        }

        [Fact]
        public async Task CreateModel_RejectsDuplicateApiIdWithinProvider()
        {
            var provider = await SeedProviderAsync();
            await DataContext.Models.AddAsync(new Model
            {
                Id = Guid.NewGuid(),
                ModelProviderId = provider.Id,
                Name = "Claude Haiku 4.5",
                ApiModelId = "claude-haiku-4-5"
            });
            await DataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<CreateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new CreateModelCommand
            {
                ModelProviderId = provider.Id,
                Name = "Haiku 4.5 (dupe)",
                ApiModelId = "claude-haiku-4-5"
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Single(DataContext.Models);
        }
    }
}
