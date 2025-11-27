namespace GSCReporter.Services.Models;

/// <summary>
/// Aggregated report containing data from multiple sources (GSC, GA, etc.)
/// </summary>
public class AggregatedReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Google Search Console data (organic search performance)
    /// </summary>
    public SearchConsoleReport? SearchConsole { get; set; }

    /// <summary>
    /// Google Analytics AI traffic data (AI-driven referrals)
    /// </summary>
    public AITrafficReport? AITraffic { get; set; }

    // Future data sources can be added here:
    // public SocialMediaReport? SocialMedia { get; set; }
    // public PaidAdsReport? PaidAds { get; set; }
}
