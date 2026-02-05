namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

/// <summary>
/// Sentinel values used for CompetitionDrive entity when source data is missing or invalid.
/// </summary>
internal static class CompetitionDriveConstants
{
    /// <summary>
    /// Fallback description used when the drive description is null or empty.
    /// </summary>
    public const string UnknownDescription = "UNKNOWN";

    /// <summary>
    /// Sentinel value used when the drive sequence number is null or missing.
    /// Indicates that the drive lacks proper sequence ordering information.
    /// </summary>
    public const string UnknownSequence = "-1";
}
