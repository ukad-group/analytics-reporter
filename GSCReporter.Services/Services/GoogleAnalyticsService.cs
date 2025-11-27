using Google.Analytics.Data.V1Beta;
using Google.Apis.Auth.OAuth2;
using GSCReporter.Services.Configuration;
using GSCReporter.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GSCReporter.Services.Services;

public class GoogleAnalyticsService : IGoogleAnalyticsService
{
    private readonly GoogleAnalyticsConfig _config;
    private readonly ILogger<GoogleAnalyticsService> _logger;
    private readonly BetaAnalyticsDataClient _client;

    // AI source mapping - domains that indicate AI-driven traffic
    private static readonly Dictionary<string, string> AISourceMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // ChatGPT / OpenAI
        { "chatgpt.com", "ChatGPT" },
        { "chat.openai.com", "ChatGPT" },
        { "openai.com", "ChatGPT" },

        // Perplexity
        { "perplexity.ai", "Perplexity" },

        // Claude / Anthropic
        { "claude.ai", "Claude" },
        { "anthropic.com", "Claude" },

        // Gemini / Google AI
        { "gemini.google.com", "Gemini" },
        { "bard.google.com", "Gemini" },

        // Microsoft Copilot
        { "copilot.microsoft.com", "Copilot" },
        { "bing.com/chat", "Copilot" },

        // Other AI tools
        { "you.com", "Other AI" },
        { "phind.com", "Other AI" },
        { "poe.com", "Other AI" },
        { "huggingface.co", "Other AI" },
        { "meta.ai", "Other AI" },
        { "pi.ai", "Other AI" }
    };

    // Map GA country names to display names (matching GSC report)
    private static readonly Dictionary<string, string> CountryDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "United States", "USA" },
        { "United Kingdom", "UK" },
        { "Ireland", "Ireland" },
        { "Belgium", "Belgium" },
        { "Netherlands", "Netherlands" },
        { "Sweden", "Sweden" },
        { "Denmark", "Denmark" },
        { "Norway", "Norway" },
        { "Finland", "Finland" }
    };

    public GoogleAnalyticsService(IOptions<AppConfig> config, ILogger<GoogleAnalyticsService> logger)
    {
        _config = config.Value.GoogleAnalytics;
        _logger = logger;
        _client = InitializeClient();
    }

    private BetaAnalyticsDataClient InitializeClient()
    {
        GoogleCredential credential;

        // Try JSON content first (for Azure deployment), then fall back to file path (for local development)
        if (!string.IsNullOrEmpty(_config.ServiceAccountKeyJson))
        {
            credential = GoogleCredential.FromJson(_config.ServiceAccountKeyJson)
                .CreateScoped("https://www.googleapis.com/auth/analytics.readonly");
        }
        else if (!string.IsNullOrEmpty(_config.ServiceAccountKeyPath))
        {
            credential = GoogleCredential.FromFile(_config.ServiceAccountKeyPath)
                .CreateScoped("https://www.googleapis.com/auth/analytics.readonly");
        }
        else
        {
            throw new InvalidOperationException("Either ServiceAccountKeyJson or ServiceAccountKeyPath must be provided for Google Analytics");
        }

        var builder = new BetaAnalyticsDataClientBuilder
        {
            Credential = credential
        };

        return builder.Build();
    }

    public async Task<AITrafficReport> GetAITrafficReportAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Fetching Google Analytics AI traffic data for {StartDate} - {EndDate}",
                startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

            // Calculate previous period dates (same duration, ending the day before current period starts)
            var periodLength = (endDate - startDate).Days + 1;
            var previousEndDate = startDate.AddDays(-1);
            var previousStartDate = previousEndDate.AddDays(-(periodLength - 1));

            _logger.LogInformation("Fetching comparison data for {PrevStartDate} - {PrevEndDate}",
                previousStartDate.ToString("yyyy-MM-dd"), previousEndDate.ToString("yyyy-MM-dd"));

            // Fetch current and previous period data by country and source (all countries, then categorize)
            var currentData = await FetchAllTrafficDataByCountryAsync(startDate, endDate);
            var previousData = await FetchAllTrafficDataByCountryAsync(previousStartDate, previousEndDate);

            // Categorize into target markets + "Other"
            var currentCategorized = CategorizeByTargetMarkets(currentData);
            var previousCategorized = CategorizeByTargetMarkets(previousData);

            var totalSessions = currentCategorized.Values.SelectMany(v => v.Values).Sum();
            var countryCount = currentCategorized.Count;

            _logger.LogInformation("Retrieved AI traffic data: {Countries} countries, {TotalSessions} total sessions",
                countryCount, totalSessions);

            return new AITrafficReport
            {
                StartDate = startDate,
                EndDate = endDate,
                SessionsByCountryAndSource = currentCategorized,
                PreviousSessionsByCountryAndSource = previousCategorized
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Google Analytics AI traffic data");
            throw;
        }
    }

    private async Task<Dictionary<string, Dictionary<string, long>>> FetchAllTrafficDataByCountryAsync(DateTime startDate, DateTime endDate)
    {
        var propertyId = $"properties/{_config.PropertyId}";

        // Fetch ALL countries (no filter) - we'll categorize into target markets + "Other" later
        var request = new RunReportRequest
        {
            Property = propertyId,
            DateRanges =
            {
                new DateRange
                {
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                }
            },
            Dimensions =
            {
                new Dimension { Name = "country" },
                new Dimension { Name = "sessionSource" }
            },
            Metrics =
            {
                new Metric { Name = "sessions" }
            },
            Limit = 10000
        };

        var response = await _client.RunReportAsync(request);

        // Result: Country (display name) -> AI Source -> Sessions
        var result = new Dictionary<string, Dictionary<string, long>>(StringComparer.OrdinalIgnoreCase);

        if (response.Rows != null)
        {
            foreach (var row in response.Rows)
            {
                var gaCountry = row.DimensionValues[0].Value;
                var source = row.DimensionValues[1].Value;
                var sessions = long.Parse(row.MetricValues[0].Value);

                // Check if this is an AI source
                var aiCategory = GetAICategory(source);
                if (aiCategory == null)
                    continue;

                // Convert GA country name to display name
                var displayCountry = CountryDisplayNames.GetValueOrDefault(gaCountry, gaCountry);

                if (!result.ContainsKey(displayCountry))
                {
                    result[displayCountry] = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                }

                if (!result[displayCountry].ContainsKey(aiCategory))
                {
                    result[displayCountry][aiCategory] = 0;
                }

                result[displayCountry][aiCategory] += sessions;
            }
        }

        return result;
    }

    private string? GetAICategory(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        // Direct match
        if (AISourceMapping.TryGetValue(source, out var category))
            return category;

        // Partial match (for cases like "chatgpt.com" appearing as just domain)
        foreach (var mapping in AISourceMapping)
        {
            if (source.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
                return mapping.Value;
        }

        return null;
    }

    /// <summary>
    /// Categorizes country data into target markets + "Other" bucket
    /// </summary>
    private static Dictionary<string, Dictionary<string, long>> CategorizeByTargetMarkets(
        Dictionary<string, Dictionary<string, long>> rawData)
    {
        var targetDisplayNames = CountryDisplayNames.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, Dictionary<string, long>>(StringComparer.OrdinalIgnoreCase);
        var otherSources = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var (country, sources) in rawData)
        {
            if (targetDisplayNames.Contains(country))
            {
                // Target market - keep as-is
                result[country] = new Dictionary<string, long>(sources, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // Non-target market - aggregate into "Other"
                foreach (var (source, sessions) in sources)
                {
                    if (!otherSources.ContainsKey(source))
                        otherSources[source] = 0;
                    otherSources[source] += sessions;
                }
            }
        }

        // Add "Other" if there's any data
        if (otherSources.Count > 0)
        {
            result["Other"] = otherSources;
        }

        return result;
    }
}
