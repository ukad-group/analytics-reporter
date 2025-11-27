using GSCReporter.Services.Models;

namespace GSCReporter.Services.Services;

public interface ISlackService
{
    Task SendReportAsync(AggregatedReport report);
}