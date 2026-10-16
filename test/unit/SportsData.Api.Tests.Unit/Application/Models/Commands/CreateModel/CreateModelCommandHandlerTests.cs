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

        private async Task<ModelFamily> SeedFamilyAsync(string name = "Claude Haiku")
        {
            var family = new ModelFamily { Id = Guid.NewGuid(), Name = name };
            await DataContext.ModelFamilies.AddAsync(family);
            await DataContext.SaveChangesAsync();
            return family;
        }

        private async Task<Model> SeedSeatAsync(ModelProvider provider, ModelFamily family, string name, bool isActive = true, ModelGateway gateway = ModelGateway.OpenRouter)
        {
            var model = new Model
            {
                Id = Guid.NewGuid(),
                ModelProviderId = provider.Id,
                ModelFamilyId = family.Id,
                Name = name,
                ApiModelId = name.ToLowerInvariant().Replace(' ', '-'),
                Gateway = gateway,
                IsActive = isActive
            };
            await DataContext.Models.AddAsync(model);
            await DataContext.SaveChangesAsync();
            return model;
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
            var family = await SeedFamilyAsync();
            var incumbent = new Model
            {
                Id = Guid.NewGuid(),
                ModelProviderId = provider.Id,
                ModelFamilyId = family.Id,
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
                ModelFamilyId = family.Id,
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

        [Fact]
        public async Task CreateModel_RequiresExistingFamily()
        {
            var provider = await SeedProviderAsync();
            var sut = Mocker.CreateInstance<CreateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new CreateModelCommand
            {
                ModelProviderId = provider.Id,
                ModelFamilyId = Guid.NewGuid(),
                Name = "Claude Haiku 4.5",
                ApiModelId = "claude-haiku-4-5"
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(DataContext.Models);
        }

        [Fact]
        public async Task CreateModel_OpenRouterVersion_TakesTheFamilySeat()
        {
            var provider = await SeedProviderAsync("Alibaba", ModelProviderKind.GatewayOnly);
            var qwen = await SeedFamilyAsync("Qwen");
            var incumbent = await SeedSeatAsync(provider, qwen, "Qwen 3 Max");
            var sut = Mocker.CreateInstance<CreateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new CreateModelCommand
            {
                ModelProviderId = provider.Id,
                ModelFamilyId = qwen.Id,
                Name = "Qwen 3.8 Max",
                ApiModelId = "qwen/qwen3.8-max-0902",
                Gateway = ModelGateway.OpenRouter
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var seat = Assert.Single(DataContext.Models.Where(m => m.ModelFamilyId == qwen.Id && m.IsActive));
            Assert.Equal(result.Value, seat.Id);
            Assert.False(DataContext.Models.Single(m => m.Id == incumbent.Id).IsActive);
        }

        [Fact]
        public async Task CreateModel_DirectRoute_LeavesTheFamilySeatAlone()
        {
            // The direct production DeepSeek row shares the DeepSeek family
            // without occupying its lab seat.
            var provider = await SeedProviderAsync("DeepSeek", ModelProviderKind.DeepSeek);
            var deepSeek = await SeedFamilyAsync("DeepSeek");
            var seat = await SeedSeatAsync(provider, deepSeek, "DeepSeek V3.1");
            var sut = Mocker.CreateInstance<CreateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new CreateModelCommand
            {
                ModelProviderId = provider.Id,
                ModelFamilyId = deepSeek.Id,
                Name = "DeepSeek Chat (direct)",
                ApiModelId = "deepseek-chat",
                Gateway = ModelGateway.None
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(DataContext.Models.Single(m => m.Id == seat.Id).IsActive);
        }

        [Theory]
        [InlineData(ModelReasoningEffort.Low, null)]
        [InlineData(null, 4096)]
        public async Task CreateModel_OpenRouter_PersistsReasoningCap(ModelReasoningEffort? effort, int? maxTokens)
        {
            var provider = await SeedProviderAsync("Alibaba", ModelProviderKind.GatewayOnly);
            var family = await SeedFamilyAsync("Qwen");
            var sut = Mocker.CreateInstance<CreateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new CreateModelCommand
            {
                ModelProviderId = provider.Id,
                ModelFamilyId = family.Id,
                Name = "Qwen 3.8 Max (0902)",
                ApiModelId = "qwen/qwen3.8-max-0902",
                Gateway = ModelGateway.OpenRouter,
                ReasoningEffort = effort,
                ReasoningMaxTokens = maxTokens
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var saved = DataContext.Models.Single(m => m.Id == result.Value);
            Assert.Equal(effort, saved.ReasoningEffort);
            Assert.Equal(maxTokens, saved.ReasoningMaxTokens);
        }

        [Theory]
        [InlineData(ModelGateway.OpenRouter, ModelReasoningEffort.Low, 4096)]   // both: OpenRouter accepts one
        [InlineData(ModelGateway.OpenRouter, null, 512)]                        // below Anthropic's minimum budget
        [InlineData(ModelGateway.OpenRouter, null, 16384)]                      // leaves no room for the answer
        [InlineData(ModelGateway.OpenRouter, (ModelReasoningEffort)42, null)]   // unknown effort
        [InlineData(ModelGateway.None, ModelReasoningEffort.Low, null)]         // direct route never sends it
        public async Task CreateModel_RejectsInvalidReasoningCap(ModelGateway gateway, ModelReasoningEffort? effort, int? maxTokens)
        {
            var provider = await SeedProviderAsync();
            var family = await SeedFamilyAsync();
            var sut = Mocker.CreateInstance<CreateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new CreateModelCommand
            {
                ModelProviderId = provider.Id,
                ModelFamilyId = family.Id,
                Name = "Claude Haiku 4.5",
                ApiModelId = gateway == ModelGateway.OpenRouter ? "anthropic/claude-haiku-4.5" : "claude-haiku-4-5",
                Gateway = gateway,
                ReasoningEffort = effort,
                ReasoningMaxTokens = maxTokens
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ResultStatus.Validation, result.Status);
            Assert.Empty(DataContext.Models);
        }
    }
}
