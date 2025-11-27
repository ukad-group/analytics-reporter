using GSCReporter.Services.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GSCReporter.AzureFunctions.Functions;

public class ReportFunction
{
    private readonly ILogger<ReportFunction> _logger;
    private readonly IReportService _reportService;

    public ReportFunction(ILogger<ReportFunction> logger, IReportService reportService)
    {
        _logger = logger;
        _reportService = reportService;
    }

    [Function("BiWeeklyReport")]
    public async Task RunBiWeeklyReport([TimerTrigger("0 0 9 * * 1")] TimerInfo myTimer)
    {
        _logger.LogInformation("Bi-weekly report timer trigger executed at: {DateTime}", DateTime.Now);

        try
        {
            await _reportService.GenerateAndSendBiWeeklyReportAsync();
            _logger.LogInformation("Bi-weekly report completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate bi-weekly report");
            throw;
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {NextRun}", myTimer.ScheduleStatus.Next);
        }
    }

    [Function("ManualReport")]
    public async Task RunManualReport([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Manual report triggered via HTTP");

        try
        {
            await _reportService.GenerateAndSendBiWeeklyReportAsync();
            _logger.LogInformation("Manual report completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate manual report");
            throw;
        }
    }
}