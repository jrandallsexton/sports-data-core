namespace SportsData.Api.Application.Admin.Commands.GenerateLoadTest;

/// <summary>
/// Target service(s) for KEDA load testing.
/// </summary>
public enum LoadTestTarget
{
    /// <summary>
    /// Target Producer service only.
    /// </summary>
    Producer,

    /// <summary>
    /// Target Provider service only.
    /// </summary>
    Provider,

    /// <summary>
    /// Target both Producer and Provider services.
    /// </summary>
    Both
}
