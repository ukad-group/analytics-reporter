namespace GSCReporter.Services.Models;

public class SearchConsoleReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long TotalClicks { get; set; }
    public long TotalImpressions { get; set; }
    public double AverageCTR { get; set; }
    public double AveragePosition { get; set; }
    public List<MarketStatistics> MarketStatistics { get; set; } = new();
}

public class MarketStatistics
{
    public string Country { get; set; } = string.Empty;
    public long TotalClicks { get; set; }
    public long TotalImpressions { get; set; }
    public double AverageCTR { get; set; }
    public double AveragePosition { get; set; }
    
    // Previous period data for comparison
    public long PreviousTotalClicks { get; set; }
    public long PreviousTotalImpressions { get; set; }
    public double PreviousAverageCTR { get; set; }
    
    // Calculated comparison metrics
    public double ClicksChangePercent => PreviousTotalClicks == 0 ? 0 : 
        ((double)(TotalClicks - PreviousTotalClicks) / PreviousTotalClicks) * 100;
    
    public double ImpressionsChangePercent => PreviousTotalImpressions == 0 ? 0 : 
        ((double)(TotalImpressions - PreviousTotalImpressions) / PreviousTotalImpressions) * 100;
}

public class SearchConsoleData
{
    public DateTime Date { get; set; }
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public double CTR { get; set; }
    public double Position { get; set; }
}