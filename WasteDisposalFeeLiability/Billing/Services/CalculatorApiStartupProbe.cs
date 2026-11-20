using WasteDisposalFeeLiability.Billing.Clients;

namespace WasteDisposalFeeLiability.Billing.Services;

/// <summary>
/// Shortly after startup, makes one authenticated call to the EPR Calculator API for a run ID that
/// can never exist, so a `FileNotFoundException` response confirms connectivity and auth are working
/// </summary>
/// <param name="eprCalculatorApiClient">The client used to call the EPR Calculator API.</param>
/// <param name="logger">The logger to record the check's result on.</param>
/// <param name="startupDelay">The delay before performing the startup check.</param>
#pragma warning disable CA1848 // Use the LoggerMessage delegates
public class CalculatorApiStartupProbe(
    IEprCalculatorApiClient eprCalculatorApiClient,
    ILogger<CalculatorApiStartupProbe> logger,
    TimeSpan startupDelay)
    : BackgroundService
{
    private const int ProbeRunId = -1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(startupDelay, stoppingToken);

        await ProbeAsync(stoppingToken);
    }

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await eprCalculatorApiClient.DownloadBillingFile(ProbeRunId, cancellationToken);
            await result.Content.DisposeAsync();

            logger.LogInformation("Calculator API startup check succeeded: however unexpectedly a billing file was returned for run ID {ProbeRunId}.", ProbeRunId);
        }
        catch (FileNotFoundException)
        {
            logger.LogInformation("Calculator API startup check succeeded: calculator-api authenticated the request and correctly reported no billing file for run ID {ProbeRunId}.", ProbeRunId);
        }
        catch (Exception ex)
        {
            logger.LogInformation("Calculator API startup check failed: fss-api may not be able to reach or authenticate against calculator-api - {ExceptionMessage}", ex.Message);
        }
    }
}
