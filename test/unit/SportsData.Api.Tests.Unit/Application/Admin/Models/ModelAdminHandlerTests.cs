using Moq;

using SportsData.Api.Application.Admin.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Models
{
    public class ModelAdminHandlerTests : ApiTestBase<CreateModelCommandHandler>
    {
        private static readonly DateTime Now = new(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        public ModelAdminHandlerTests()
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

        [Fact]
        public async Task UpdateModel_EditsMetadata_NotIdentity()
        {
            var provider = await SeedProviderAsync();
            var model = new Model
            {
                Id = Guid.NewGuid(),
                ModelProviderId = provider.Id,
                Name = "Claude Sonnet 4.6",
                ApiModelId = "claude-sonnet-4-6"
            };
            await DataContext.Models.AddAsync(model);
            await DataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<UpdateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = model.Id,
                KnowledgeCutoffUtc = new DateTime(2025, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                CutoffEvidence = "docs.anthropic.com — resolved training-vs-reliable discrepancy",
                CutoffVerifiedUtc = Now,
                IsActive = true
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var updated = DataContext.Models.Single(m => m.Id == model.Id);
            Assert.Equal(new DateTime(2025, 8, 31, 0, 0, 0, DateTimeKind.Utc), updated.KnowledgeCutoffUtc);
            Assert.Equal(Now, updated.CutoffVerifiedUtc);
            // Identity untouched
            Assert.Equal("Claude Sonnet 4.6", updated.Name);
            Assert.Equal("claude-sonnet-4-6", updated.ApiModelId);
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
