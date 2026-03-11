namespace GSCReporter.Services.Models;

public class PaidAdsReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Dictionary<string, PaidAdsStatistics> StatisticsByCountry { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PaidAdsStatistics GetStatistics(string country)
    {
        return StatisticsByCountry.GetValueOrDefault(country, new PaidAdsStatistics { Country = country });
    }
}

public class PaidAdsStatistics
{
    public string Country { get; set; } = string.Empty;
    public long Sessions { get; set; }
    public long EngagedSessions { get; set; }
    public long PreviousSessions { get; set; }
    public long PreviousEngagedSessions { get; set; }

    public double EngagementRate => Sessions == 0 ? 0 : (double)EngagedSessions / Sessions;
    public double PreviousEngagementRate => PreviousSessions == 0 ? 0 : (double)PreviousEngagedSessions / PreviousSessions;

    public double SessionsChangePercent => PreviousSessions == 0 ? 0 : ((double)(Sessions - PreviousSessions) / PreviousSessions) * 100;
    public double EngagedSessionsChangePercent => PreviousEngagedSessions == 0 ? 0 : ((double)(EngagedSessions - PreviousEngagedSessions) / PreviousEngagedSessions) * 100;
}