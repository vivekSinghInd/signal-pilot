using Microsoft.Extensions.Options;

namespace StockOpportunityAutomation;

public interface IOpportunityEngine
{
    Task<OpportunityScanResult> ScanAsync(int? limit, CancellationToken cancellationToken);
}

public sealed class OpportunityEngine(
    IScannerClient scannerClient,
    IStockNewsClient newsClient,
    IOpenAiDecisionClient aiClient,
    IOptions<AutomationOptions> options,
    IOptions<PaperTradingOptions> paperOptions,
    ILogger<OpportunityEngine> logger) : IOpportunityEngine
{
    private static readonly SemaphoreSlim ScanGate = new(1, 1);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);
    private static OpportunityScanResult? CachedResult;
    private static DateTimeOffset CachedAt;
    private static int? CachedLimit;

    private readonly AutomationOptions _options = options.Value;
    private readonly PaperTradingOptions _paperOptions = paperOptions.Value;

    public async Task<OpportunityScanResult> ScanAsync(int? limit, CancellationToken cancellationToken)
    {
        var requestedLimit = Math.Clamp(limit ?? _options.MaxCandidates, 1, 50);
        if (TryGetCached(requestedLimit, out var cached))
            return cached;

        await ScanGate.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCached(requestedLimit, out cached))
                return cached;

            var fresh = await ScanFreshAsync(requestedLimit, cancellationToken);
            CachedResult = fresh;
            CachedLimit = requestedLimit;
            CachedAt = DateTimeOffset.UtcNow;
            return fresh;
        }
        finally
        {
            ScanGate.Release();
        }
    }

    private static bool TryGetCached(int requestedLimit, out OpportunityScanResult result)
    {
        if (CachedResult is not null && CachedLimit == requestedLimit && DateTimeOffset.UtcNow - CachedAt <= CacheLifetime)
        {
            result = CachedResult;
            return true;
        }

        result = null!;
        return false;
    }

    private async Task<OpportunityScanResult> ScanFreshAsync(int requestedLimit, CancellationToken cancellationToken)
    {
        var result = new OpportunityScanResult
        {
            AutomationEnabled = _options.Enabled,
            AiEnabled = aiClient.IsEnabled,
            LiveOrdersEnabled = false,
            PaperTradingEnabled = _paperOptions.Enabled && _paperOptions.PaperMode,
            MaxPaperCapitalPerTrade = _paperOptions.MaxCapitalPerTrade
        };

        if (!_options.Enabled)
            return result;

        var allSignals = await scannerClient.GetSignalsAsync(cancellationToken);
        if (allSignals.Count == 0)
            return result;

        result.LatestSignalDate = allSignals.Max(x => x.RecognizeDate);
        var latest = allSignals.Where(x => x.RecognizeDate == result.LatestSignalDate).ToList();

        var candidates = latest
            .Where(x => !string.IsNullOrWhiteSpace(x.Symbol))
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(BuildCandidate)
            .Where(IsEligible)
            .OrderByDescending(x => x.PreliminaryScore)
            .ThenByDescending(x => x.ScreenerCount)
            .Take(requestedLimit)
            .ToList();

        await EnrichNewsAsync(candidates, cancellationToken);
        var decisions = await aiClient.AnalyzeAsync(candidates, cancellationToken);
        var bySymbol = decisions.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (bySymbol.TryGetValue(candidate.Symbol, out var decision))
            {
                decision.TradeEligible = false;
                decision.ExecutionBlockedReason = "Live order execution is disabled.";
                candidate.Decision = decision;
            }
            else
            {
                candidate.Decision = Fallback(candidate, index + 1);
            }
        }

        result.Candidates = candidates;
        logger.LogInformation("Scan complete: {Count} candidates; AI enabled: {AiEnabled}", candidates.Count, result.AiEnabled);
        return result;
    }

    private OpportunityCandidate BuildCandidate(IGrouping<string, ScannerSignal> group)
    {
        var signals = group.ToList();
        var first = signals[0];
        var screeners = signals.Select(x => x.ScreenerName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var daily = signals.Any(x => x.TimeFrame.Equals("D", StringComparison.OrdinalIgnoreCase));
        var weekly = signals.Any(x => x.TimeFrame.Equals("W", StringComparison.OrdinalIgnoreCase));
        var rsi = signals.Max(x => x.Rsi);
        var weeklyRsi = signals.Max(x => x.RsiWeekly);
        var monthlyRsi = signals.Max(x => x.RsiMonthly);
        var score = 20d;

        if (screeners.Count >= 2) score += 25;
        if (daily) score += 10;
        if (weekly) score += 10;
        if (rsi is >= 55 and <= 70) score += 10;
        else if (rsi > 75) score -= 5;
        if (weeklyRsi is >= 55 and <= 70) score += 10;
        if (monthlyRsi is >= 50 and <= 70) score += 5;
        if (first.MarketCapCategory is "LargeCap" or "MidCap") score += 5;
        if (first.Volume > 0) score += 5;

        return new OpportunityCandidate
        {
            Symbol = first.Symbol,
            MarketCapCategory = first.MarketCapCategory,
            RecognizedPrice = first.RecognizedPrice,
            PreliminaryScore = Math.Clamp(score, 0, 100),
            ScreenerCount = screeners.Count,
            HasDailySignal = daily,
            HasWeeklySignal = weekly,
            MaxRsi = rsi,
            MaxWeeklyRsi = weeklyRsi,
            MaxMonthlyRsi = monthlyRsi,
            Evidence = signals.Select(x => $"{x.ScreenerName} ({x.TimeFrame})").Distinct().ToList(),
            Signals = signals
        };
    }

    private bool IsEligible(OpportunityCandidate candidate) =>
        (!_options.ExcludeMicroCap || !string.Equals(candidate.MarketCapCategory, "MicroCap", StringComparison.OrdinalIgnoreCase)) &&
        candidate.MaxWeeklyRsi > 0 &&
        candidate.PreliminaryScore >= _options.MinPreliminaryScore;

    private async Task EnrichNewsAsync(IReadOnlyList<OpportunityCandidate> candidates, CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(4);
        await Task.WhenAll(candidates.Select(async candidate =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try { candidate.News = (await newsClient.GetNewsAsync(candidate.Symbol, cancellationToken)).ToList(); }
            finally { semaphore.Release(); }
        }));
    }

    private static OpportunityDecision Fallback(OpportunityCandidate candidate, int rank) => new()
    {
        Symbol = candidate.Symbol,
        Status = candidate.PreliminaryScore >= 75 ? "RESEARCH_CANDIDATE" : "WATCHLIST",
        Conviction = (int)Math.Round(candidate.PreliminaryScore),
        ThesisSummary = "Scanner confluence passed the deterministic filter; LLM confirmation is not configured.",
        Catalyst = candidate.News.FirstOrDefault()?.Title ?? "No recent news catalyst found.",
        UpsideCase = "Multiple scanner signals and timeframe alignment may support continuation.",
        DownsideCase = "The signal can fail without price follow-through or a verified catalyst.",
        RiskFlags = candidate.MaxWeeklyRsi <= 0 ? ["Missing weekly RSI value."] : [],
        Evidence = candidate.Evidence,
        TradeEligible = false,
        ExecutionBlockedReason = "Live order execution is disabled."
    };
}
