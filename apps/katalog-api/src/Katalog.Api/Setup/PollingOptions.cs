namespace Katalog.Api.Setup;

/// <summary>
/// Configuration for the release polling background service (research §2.8: 6-24 h rhythm).
/// </summary>
public sealed class PollingOptions
{
    public const string SectionName = "Polling";

    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(12);

    public static bool Validate(PollingOptions options)
    {
        // Research §2.8: artist-release polling rhythm of 6-24 h fits the dev-mode quota.
        return options.Interval >= TimeSpan.FromHours(6) && options.Interval <= TimeSpan.FromHours(24);
    }
}
