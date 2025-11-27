namespace GSCReporter.Services.Configuration;

/// <summary>
/// Shared configuration for target markets used across GSC and GA reports
/// </summary>
public static class TargetMarkets
{
    /// <summary>
    /// Target countries with their GSC country codes and display names
    /// </summary>
    public static readonly Dictionary<string, string> Countries = new(StringComparer.OrdinalIgnoreCase)
    {
        { "usa", "USA" },
        { "gbr", "UK" },
        { "irl", "Ireland" },
        { "bel", "Belgium" },
        { "nld", "Netherlands" },
        { "swe", "Sweden" },
        { "dnk", "Denmark" },
        { "nor", "Norway" },
        { "fin", "Finland" }
    };

    /// <summary>
    /// Country names for Google Analytics filtering (GA uses full country names)
    /// </summary>
    public static readonly string[] GACountryNames =
    {
        "United States",
        "United Kingdom",
        "Ireland",
        "Belgium",
        "Netherlands",
        "Sweden",
        "Denmark",
        "Norway",
        "Finland"
    };
}
