using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StockOpportunityAutomation;

public interface IScannerClient
{
    Task<IReadOnlyList<ScannerSignal>> GetSignalsAsync(CancellationToken cancellationToken);
}

public sealed class ScannerClient(
    HttpClient httpClient,
    IOptions<ScannerOptions> options,
    ILogger<ScannerClient> logger) : IScannerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ScannerOptions _options = options.Value;

    public async Task<IReadOnlyList<ScannerSignal>> GetSignalsAsync(CancellationToken cancellationToken)
    {
        httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));

        try
        {
            var response = await httpClient.GetAsync(_options.ResultsUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var signals = await response.Content.ReadFromJsonAsync<List<ScannerSignal>>(JsonOptions, cancellationToken);
            return signals ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not fetch scanner results from {Url}", _options.ResultsUrl);
            return [];
        }
    }
}
