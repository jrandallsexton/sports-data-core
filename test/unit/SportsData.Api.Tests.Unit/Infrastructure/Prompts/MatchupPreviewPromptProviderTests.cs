using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Infrastructure.Prompts
{
    public class MatchupPreviewPromptProviderTests : ApiTestBase<MatchupPreviewPromptProvider>
    {
        private MatchupPreviewPromptProvider BuildProvider() => new(DataContext);

        private async Task<Prompt> AddPromptAsync(
            string name,
            Sport? sport,
            bool withStats,
            bool isDefault,
            string text,
            PromptType type = PromptType.MatchupPreview)
        {
            var prompt = new Prompt
            {
                Id = Guid.NewGuid(),
                Name = name,
                Type = type,
                Sport = sport,
                WithStats = withStats,
                IsDefault = isDefault,
                Text = text
            };
            await DataContext.Prompts.AddAsync(prompt);
            await DataContext.SaveChangesAsync();
            return prompt;
        }

        [Fact]
        public async Task ResolvesAnySportDefault_WhenNoSportSpecificExists()
        {
            await AddPromptAsync("prediction-insights-v1", sport: null, withStats: false, isDefault: true, "GENERIC");

            var prompt = await BuildProvider().GetPromptAsync(
                new PreviewPromptRequest(Sport.FootballNfl, HasStats: false));

            Assert.Equal("GENERIC", prompt.PromptText);
            Assert.Equal("prediction-insights-v1", prompt.PromptName);
        }

        [Fact]
        public async Task PrefersSportSpecificDefault_OverAnySport()
        {
            await AddPromptAsync("prediction-insights-v1", sport: null, withStats: false, isDefault: true, "GENERIC");
            await AddPromptAsync("prediction-insights-v1-nfl", Sport.FootballNfl, withStats: false, isDefault: true, "NFL VOICE");

            var nfl = await BuildProvider().GetPromptAsync(
                new PreviewPromptRequest(Sport.FootballNfl, HasStats: false));
            Assert.Equal("NFL VOICE", nfl.PromptText);

            // Other sports still get the any-sport default.
            var ncaa = await BuildProvider().GetPromptAsync(
                new PreviewPromptRequest(Sport.FootballNcaa, HasStats: false));
            Assert.Equal("GENERIC", ncaa.PromptText);
        }

        [Fact]
        public async Task ResolvesTheCorrectStatsSlot()
        {
            await AddPromptAsync("no-stats", sport: null, withStats: false, isDefault: true, "NO STATS");
            await AddPromptAsync("with-stats", sport: null, withStats: true, isDefault: true, "WITH STATS");

            var withStats = await BuildProvider().GetPromptAsync(
                new PreviewPromptRequest(Sport.FootballNfl, HasStats: true));
            Assert.Equal("WITH STATS", withStats.PromptText);

            var noStats = await BuildProvider().GetPromptAsync(
                new PreviewPromptRequest(Sport.FootballNfl, HasStats: false));
            Assert.Equal("NO STATS", noStats.PromptText);
        }

        [Fact]
        public async Task IgnoresNonDefaultPrompts_ForSlotResolution()
        {
            await AddPromptAsync("old-version", sport: null, withStats: false, isDefault: false, "OLD");
            await AddPromptAsync("current", sport: null, withStats: false, isDefault: true, "CURRENT");

            var prompt = await BuildProvider().GetPromptAsync(
                new PreviewPromptRequest(Sport.FootballNfl, HasStats: false));

            Assert.Equal("CURRENT", prompt.PromptText);
        }

        [Fact]
        public async Task Throws_WhenNoDefaultConfigured()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BuildProvider().GetPromptAsync(
                    new PreviewPromptRequest(Sport.FootballNfl, HasStats: true)));

            Assert.Contains("No default matchup-preview prompt", ex.Message);
        }

        [Fact]
        public async Task PromptIdOverride_BypassesDefaultResolution()
        {
            await AddPromptAsync("the-default", sport: null, withStats: true, isDefault: true, "DEFAULT");
            var experimental = await AddPromptAsync("with-history-v1", sport: null, withStats: true, isDefault: false, "EXPERIMENTAL");

            var prompt = await BuildProvider().GetPromptAsync(
                new PreviewPromptRequest(Sport.FootballNfl, HasStats: true, PromptId: experimental.Id));

            Assert.Equal("EXPERIMENTAL", prompt.PromptText);
            Assert.Equal("with-history-v1", prompt.PromptName);
            Assert.Equal(experimental.Id, prompt.PromptId);
        }

        [Fact]
        public async Task PromptIdOverride_Throws_WhenPromptMissing()
        {
            // An operator typo must fail loudly, never silently fall back.
            await AddPromptAsync("the-default", sport: null, withStats: true, isDefault: true, "DEFAULT");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BuildProvider().GetPromptAsync(
                    new PreviewPromptRequest(Sport.FootballNfl, HasStats: true, PromptId: Guid.NewGuid())));

            Assert.Contains("does not exist", ex.Message);
        }

        [Fact]
        public async Task PromptIdOverride_Throws_ForWrongPromptType()
        {
            var recap = await AddPromptAsync("game-recap-v2", sport: null, withStats: false, isDefault: false, "RECAP", PromptType.GameRecap);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BuildProvider().GetPromptAsync(
                    new PreviewPromptRequest(Sport.FootballNfl, HasStats: false, PromptId: recap.Id)));

            Assert.Contains("GameRecap", ex.Message);
        }
    }
}
