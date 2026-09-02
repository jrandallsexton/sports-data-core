using SportsData.Core.Common;

using System.Collections.Generic;

namespace SportsData.Api.Application.Previews;

/// <summary>
/// Which sports may generate LLM matchup previews.
/// </summary>
/// <remarks>
/// This is an engineering fact, not an ops toggle (contrast the
/// ApiConfig.MatchupPreviewGenerationEnabled kill-switch): preview prompts
/// exist ONLY for football. An unlisted sport falls through prompt
/// resolution to the any-sport default and burns model tokens producing
/// football-shaped text for the wrong sport. BaseballMlb in particular is a
/// live-pipeline test sport — throwaway single-day leagues must never spend
/// DeepSeek tokens (operator, 2026-09-02). Enforced at the single choke
/// point (MatchupPreviewProcessor, which every producer funnels through)
/// with cheap early-outs in the league event handlers; add a sport here
/// only when its prompts actually exist.
/// </remarks>
public static class MatchupPreviewPolicy
{
    private static readonly HashSet<Sport> SupportedSports =
    [
        Sport.FootballNcaa,
        Sport.FootballNfl
    ];

    public static bool SupportsSport(Sport sport) => SupportedSports.Contains(sport);
}
