using GSCReporter.Services.Configuration;
using GSCReporter.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Slack.Webhooks;

namespace GSCReporter.Services.Services;

public class SlackService : ISlackService
{
    private readonly SlackConfig _config;
    private readonly ILogger<SlackService> _logger;
    private readonly SlackClient _slackClient;

    // Major AI platforms to show as columns
    private static readonly string[] MajorAIPlatforms = { "ChatGPT", "Perplexity", "Claude", "Gemini", "Copilot" };

    public SlackService(IOptions<AppConfig> config, ILogger<SlackService> logger)
    {
        _config = config.Value.Slack;
        _logger = logger;
        _slackClient = new SlackClient(_config.WebhookUrl);
    }

    public async Task SendReportAsync(AggregatedReport report)
    {
        try
        {
            _logger.LogInformation("Sending report to Slack for period {StartDate} - {EndDate}",
                report.StartDate.ToString("dd.MM.yyyy"), report.EndDate.ToString("dd.MM.yyyy"));

            var message = FormatReportMessage(report);

            var slackMessage = new SlackMessage
            {
                Channel = _config.Channel,
                Text = message,
                Username = "GSC Reporter"
            };

            var result = await _slackClient.PostAsync(slackMessage);

            if (result)
            {
                _logger.LogInformation("Report sent successfully to Slack");
            }
            else
            {
                _logger.LogError("Failed to send report to Slack");
                throw new Exception("Failed to send message to Slack");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending report to Slack");
            throw;
        }
    }

    private string FormatReportMessage(AggregatedReport report)
    {
        var periodText = $"{report.StartDate:dd.MM.yyyy} - {report.EndDate:dd.MM.yyyy}";

        var message = $"📊 *Performance Report*\n\n{periodText}\n";

        // Add summary if GSC data available
        if (report.SearchConsole != null)
        {
            var clicksText = FormatNumber(report.SearchConsole.TotalClicks);
            var impressionsText = FormatNumber(report.SearchConsole.TotalImpressions);

            var targetCountries = TargetMarkets.Countries;
            var targetMarkets = report.SearchConsole.MarketStatistics
                .Where(m => targetCountries.ContainsKey(m.Country.ToLower()))
                .ToList();
            var marketClicks = targetMarkets.Sum(m => m.TotalClicks);
            var marketImpressions = targetMarkets.Sum(m => m.TotalImpressions);
            var marketCTR = marketImpressions > 0 ? (double)marketClicks / marketImpressions : 0;
            var marketPosition = marketImpressions > 0 ?
                targetMarkets.Sum(m => m.AveragePosition * m.TotalImpressions) / marketImpressions : 0;

            message += $@"
{clicksText} - clicks
{impressionsText} - impressions

Target markets = {string.Join(", ", targetCountries.Values)}
Average CTR by target markets: {marketCTR:P2}
Average position in target markets: {marketPosition:F1}";
        }

        // Create merged table with GSC + AI traffic data
        message += FormatMergedTable(report);

        return message;
    }

    private string FormatMergedTable(AggregatedReport report)
    {
        var targetCountries = TargetMarkets.Countries;
        var gscReport = report.SearchConsole;
        var aiTraffic = report.AITraffic;

        // Determine which AI columns to show (only those with data)
        var aiColumnsToShow = GetAIColumnsWithData(aiTraffic);

        // Build header
        var message = "\n\n🌍 *Markets Performance (vs Previous Period):*\n```\n";

        // Dynamic header based on AI columns
        var header = "Country     | Clicks      | Impressions   | CTR   ";
        foreach (var aiCol in aiColumnsToShow)
        {
            header += $"| {aiCol,-10}";
        }
        message += header + "\n";

        // Separator
        var separator = "------------|-------------|---------------|-------";
        foreach (var _ in aiColumnsToShow)
        {
            separator += "|----------";
        }
        message += separator + "\n";

        // Build rows for each target country
        var rows = new List<(string Country, MarketStatistics? Market, bool IsOther)>();

        foreach (var (countryCode, countryName) in targetCountries)
        {
            var market = gscReport?.MarketStatistics.FirstOrDefault(m =>
                string.Equals(m.Country, countryCode, StringComparison.OrdinalIgnoreCase));

            if (market != null)
            {
                rows.Add((countryName, new MarketStatistics
                {
                    Country = countryName,
                    TotalClicks = market.TotalClicks,
                    TotalImpressions = market.TotalImpressions,
                    AverageCTR = market.AverageCTR,
                    AveragePosition = market.AveragePosition,
                    PreviousTotalClicks = market.PreviousTotalClicks,
                    PreviousTotalImpressions = market.PreviousTotalImpressions,
                    PreviousAverageCTR = market.PreviousAverageCTR
                }, false));
            }
        }

        // Calculate "Other" totals for GSC
        if (gscReport != null)
        {
            var otherMarkets = gscReport.MarketStatistics.Where(m =>
                !targetCountries.ContainsKey(m.Country.ToLower())).ToList();

            if (otherMarkets.Count > 0)
            {
                rows.Add(("Other", new MarketStatistics
                {
                    Country = "Other",
                    TotalClicks = otherMarkets.Sum(m => m.TotalClicks),
                    TotalImpressions = otherMarkets.Sum(m => m.TotalImpressions),
                    AverageCTR = otherMarkets.Average(m => m.AverageCTR),
                    PreviousTotalClicks = otherMarkets.Sum(m => m.PreviousTotalClicks),
                    PreviousTotalImpressions = otherMarkets.Sum(m => m.PreviousTotalImpressions)
                }, true));
            }
        }

        // Sort by clicks descending, but "Other" always goes last
        foreach (var row in rows.OrderBy(r => r.IsOther ? 1 : 0)
                                .ThenByDescending(r => r.Market?.TotalClicks ?? 0))
        {
            var market = row.Market;
            var countryName = row.Country;

            var formattedClicks = market != null ? FormatNumberWithChange(market.TotalClicks, market.ClicksChangePercent) : "-";
            var formattedImpr = market != null ? FormatNumberWithChange(market.TotalImpressions, market.ImpressionsChangePercent) : "-";
            var ctr = market != null ? $"{market.AverageCTR:P1}" : "-";

            var rowText = $"{countryName,-11} | {formattedClicks,-11} | {formattedImpr,-13} | {ctr,-5} ";

            // Add AI traffic columns
            foreach (var aiCol in aiColumnsToShow)
            {
                var sessions = aiTraffic?.GetSessions(countryName, aiCol) ?? 0;
                var prevSessions = aiTraffic?.GetPreviousSessions(countryName, aiCol) ?? 0;

                if (sessions > 0 || prevSessions > 0)
                {
                    // Skip if current is 0 (even if previous had data) - show as "-"
                    if (sessions == 0)
                    {
                        rowText += $"| {"-",-10}";
                    }
                    else
                    {
                        var changePercent = prevSessions == 0 ? 0 : ((double)(sessions - prevSessions) / prevSessions) * 100;
                        rowText += $"| {FormatNumberWithChange(sessions, changePercent),-10}";
                    }
                }
                else
                {
                    rowText += $"| {"-",-10}";
                }
            }

            message += rowText + "\n";
        }

        message += "```";
        return message;
    }

    private static List<string> GetAIColumnsWithData(AITrafficReport? aiTraffic)
    {
        if (aiTraffic == null)
            return [];

        var columnsWithData = new List<string>();

        // Check each major platform for data - only if current period has sessions > 0
        foreach (var platform in MajorAIPlatforms)
        {
            var hasCurrentData = aiTraffic.SessionsByCountryAndSource.Values
                .Any(sources => sources.ContainsKey(platform) && sources[platform] > 0);

            if (hasCurrentData)
            {
                columnsWithData.Add(platform);
            }
        }

        // Check for "Other AI" data in current period
        var otherAISources = aiTraffic.GetAllSources()
            .Where(s => !MajorAIPlatforms.Contains(s))
            .ToList();

        if (otherAISources.Count > 0)
        {
            // Check if any "Other AI" source has current sessions > 0
            var hasOtherData = aiTraffic.SessionsByCountryAndSource.Values
                .Any(sources => otherAISources.Any(os => sources.ContainsKey(os) && sources[os] > 0));

            if (hasOtherData)
            {
                columnsWithData.Add("Other");
            }
        }

        return columnsWithData;
    }

    private string FormatNumber(long number)
    {
        return number switch
        {
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString()
        };
    }

    private string FormatPercentageChange(double changePercent)
    {
        if (Math.Abs(changePercent) < 0.1)
            return "0%";

        var sign = changePercent > 0 ? "+" : "";
        return $"{sign}{changePercent:F0}%";
    }

    private string FormatNumberWithChange(long number, double changePercent)
    {
        var numberText = FormatNumber(number);
        var changeText = FormatPercentageChange(changePercent);
        return $"{numberText} ({changeText})";
    }
}
