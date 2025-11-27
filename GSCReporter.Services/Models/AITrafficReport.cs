namespace GSCReporter.Services.Models;

public class AITrafficReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// AI traffic data grouped by country, then by AI source
    /// Key: Country name (e.g., "USA", "UK"), Value: sessions per AI source
    /// </summary>
    public Dictionary<string, Dictionary<string, long>> SessionsByCountryAndSource { get; set; } = new();

    /// <summary>
    /// Previous period data for comparison
    /// </summary>
    public Dictionary<string, Dictionary<string, long>> PreviousSessionsByCountryAndSource { get; set; } = new();

    /// <summary>
    /// Get sessions for a specific country and AI source
    /// </summary>
    public long GetSessions(string country, string source)
    {
        if (SessionsByCountryAndSource.TryGetValue(country, out var sources))
        {
            return sources.GetValueOrDefault(source, 0);
        }
        return 0;
    }

    /// <summary>
    /// Get previous period sessions for a specific country and AI source
    /// </summary>
    public long GetPreviousSessions(string country, string source)
    {
        if (PreviousSessionsByCountryAndSource.TryGetValue(country, out var sources))
        {
            return sources.GetValueOrDefault(source, 0);
        }
        return 0;
    }

    /// <summary>
    /// Get total sessions for a country across all AI sources
    /// </summary>
    public long GetTotalSessionsForCountry(string country)
    {
        if (SessionsByCountryAndSource.TryGetValue(country, out var sources))
        {
            return sources.Values.Sum();
        }
        return 0;
    }

    /// <summary>
    /// Get all AI sources that have data
    /// </summary>
    public IEnumerable<string> GetAllSources()
    {
        return SessionsByCountryAndSource.Values
            .SelectMany(s => s.Keys)
            .Union(PreviousSessionsByCountryAndSource.Values.SelectMany(s => s.Keys))
            .Distinct();
    }
}
