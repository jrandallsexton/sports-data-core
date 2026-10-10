using Microsoft.Extensions.Configuration;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

/// <summary>
/// Instant-analysis kill switch. Off unless
/// <c>SportsData.Producer:InstantAnalysis:Enabled</c> is "true" in the
/// pod's sport label (Producer pods are single-sport, so the label keys it
/// per sport).
///
/// Read once at startup (App Configuration isn't refreshed at runtime), so
/// flipping it needs a pod restart. Day-to-day rollout is per game via
/// <c>CompetitionStream.LiveAnalysisEnabled</c>, which takes effect
/// immediately.
/// </summary>
public class InstantAnalysisConfig
{
    public const string EnabledKey = "SportsData.Producer:InstantAnalysis:Enabled";

    public bool Enabled { get; init; }

    public static InstantAnalysisConfig FromConfiguration(IConfiguration config) =>
        new()
        {
            Enabled = bool.TryParse(config[EnabledKey], out var enabled) && enabled
        };
}
