using GSCReporter.Services.Models;

namespace GSCReporter.Services.Services;

public interface IGoogleAnalyticsService
{
    Task<AITrafficReport> GetAITrafficReportAsync(DateTime startDate, DateTime endDate);
}
