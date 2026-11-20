using System.Text.Json.Serialization;

namespace StockOpportunityAutomation;

public sealed class ScannerOptions
{
    public string ResultsUrl { get; set; } = "https://screener.getvoroa.com/api/screener/results";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class NewsOptions
{
    public bool Enabled { get; set; } = true;
    public int LookbackHours { get; set; } = 48;
    public int MaxArticlesPerSymbol { get; set; } = 5;
    public int Concurrency { get; set; } = 4;
    public int TimeoutSeconds { get; set; } = 8;
}

public sealed class OpenAiOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com";
    public string Model { get; set; } = "gpt-6.2-astra";
    public string ReasoningEffort { get; set; } = "high";
}

public sealed class AutomationOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxCandidates { get; set; } = 12;
    public int MinPreliminaryScore { get; set; } = 60;
    public bool ExcludeMicroCap { get; set; } = true;
    public int IntervalMinutes { get; set; } = 15;
}

public sealed class PaperTradingOptions
{
    public bool Enabled { get; set; } = true;
    public bool PaperMode { get; set; } = true;
    public decimal MaxCapitalPerTrade { get; set; } = 20000;
    public int MaxOpenPositions { get; set; } = 3;
}

public sealed class ScannerSignal
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty;
    [JsonPropertyName("screenerName")] public string ScreenerName { get; set; } = string.Empty;
    [JsonPropertyName("timeFrame")] public string TimeFrame { get; set; } = string.Empty;
    [JsonPropertyName("recognizeDate")] public DateTime RecognizeDate { get; set; }
    [JsonPropertyName("rsi")] public double Rsi { get; set; }
    [JsonPropertyName("rsiWeekly")] public double RsiWeekly { get; set; }
    [JsonPropertyName("rsiMonthly")] public double RsiMonthly { get; set; }
    [JsonPropertyName("volume")] public double Volume { get; set; }
    [JsonPropertyName("recognizedPrice")] public double RecognizedPrice { get; set; }
    [JsonPropertyName("marketCapCategory")] public string? MarketCapCategory { get; set; }
}

public sealed class NewsArticle
{
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class OpportunityCandidate
{
    public string Symbol { get; set; } = string.Empty;
    public string? MarketCapCategory { get; set; }
    public double RecognizedPrice { get; set; }
    public double PreliminaryScore { get; set; }
    public int ScreenerCount { get; set; }
    public bool HasDailySignal { get; set; }
    public bool HasWeeklySignal { get; set; }
    public double MaxRsi { get; set; }
    public double MaxWeeklyRsi { get; set; }
    public double MaxMonthlyRsi { get; set; }
    public List<string> Evidence { get; set; } = [];
    public List<ScannerSignal> Signals { get; set; } = [];
    public List<NewsArticle> News { get; set; } = [];
    public OpportunityDecision Decision { get; set; } = new();
}

public sealed class OpportunityDecision
{
    public string Symbol { get; set; } = string.Empty;
    public string Status { get; set; } = "WATCHLIST";
    public int Conviction { get; set; }
    public string ThesisSummary { get; set; } = string.Empty;
    public string Catalyst { get; set; } = string.Empty;
    public string UpsideCase { get; set; } = string.Empty;
    public string DownsideCase { get; set; } = string.Empty;
    public List<string> RiskFlags { get; set; } = [];
    public List<string> Evidence { get; set; } = [];
    public bool TradeEligible { get; set; }
    public string ExecutionBlockedReason { get; set; } = "Live order execution is disabled.";
}

public sealed class OpportunityScanResult
{
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTime? LatestSignalDate { get; set; }
    public bool AutomationEnabled { get; set; }
    public bool AiEnabled { get; set; }
    public bool LiveOrdersEnabled { get; set; }
    public bool PaperTradingEnabled { get; set; }
    public decimal MaxPaperCapitalPerTrade { get; set; }
    public List<OpportunityCandidate> Candidates { get; set; } = [];
}
