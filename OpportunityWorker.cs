using Microsoft.Extensions.Options;

namespace StockOpportunityAutomation;

public sealed class OpportunityWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationOptions> options,
    ILogger<OpportunityWorker> logger) : BackgroundService
{
    private readonly AutomationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Clamp(_options.IntervalMinutes, 1, 60)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!_options.Enabled || !IsIndianMarketWindow())
                continue;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<IOpportunityEngine>();
                var result = await engine.ScanAsync(_options.MaxCandidates, stoppingToken);
                var top = result.Candidates.FirstOrDefault();
                if (top is not null)
                    logger.LogInformation("Top opportunity: {Symbol}, score {Score}, status {Status}", top.Symbol, top.PreliminaryScore, top.Decision.Status);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Scheduled opportunity scan failed"); }
        }
    }

    private static bool IsIndianMarketWindow()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        return now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
               now.TimeOfDay >= TimeSpan.FromHours(9.25) &&
               now.TimeOfDay <= new TimeSpan(15, 30, 0);
    }
}
