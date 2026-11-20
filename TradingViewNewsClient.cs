using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace StockOpportunityAutomation;

public interface IStockNewsClient
{
    Task<IReadOnlyList<NewsArticle>> GetNewsAsync(string symbol, CancellationToken cancellationToken);
}

public sealed class TradingViewNewsClient : IStockNewsClient
{
    private readonly HttpClient _httpClient;
    private readonly NewsOptions _options;
    private readonly ILogger<TradingViewNewsClient> _logger;

    public TradingViewNewsClient(
        HttpClient httpClient,
        IOptions<NewsOptions> options,
        ILogger<TradingViewNewsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 2, 20));
    }

    public async Task<IReadOnlyList<NewsArticle>> GetNewsAsync(string symbol, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return [];

        var tvSymbol = $"NSE:{symbol.ToUpperInvariant()}";
        var encoded = Uri.EscapeDataString($"symbol:{tvSymbol}");
        var url = $"https://news-mediator.tradingview.com/public/view/v1/symbol?filter=lang%3Aen&filter={encoded}&client=landing&streaming=false&user_prostatus=non_pro";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var since = DateTimeOffset.UtcNow.AddHours(-Math.Max(1, _options.LookbackHours));

            return Walk(document.RootElement)
                .Select(TryReadArticle)
                .Where(x => x is not null)
                .Select(x => x!)
                .Where(x => x.PublishedAt is null || x.PublishedAt >= since)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Url) ? x.Title : x.Url, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
                .Take(Math.Max(1, _options.MaxArticlesPerSymbol))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch news for {Symbol}", symbol);
            return [];
        }
    }

    private static IEnumerable<JsonElement> Walk(JsonElement element)
    {
        yield return element;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in Walk(property.Value))
                    yield return child;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                foreach (var nested in Walk(child))
                    yield return nested;
            }
        }
    }

    private static NewsArticle? TryReadArticle(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("title", out var titleProperty))
            return null;

        var title = titleProperty.GetString();
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var url = ReadString(element, "url") ?? ReadString(element, "link") ?? ReadString(element, "storyPath") ?? string.Empty;
        if (url.StartsWith('/'))
            url = "https://www.tradingview.com" + url;

        var published = ReadDate(element, "published") ?? ReadDate(element, "publishedTime") ?? ReadDate(element, "date") ?? ReadDate(element, "time");
        return new NewsArticle
        {
            Title = title,
            Source = ReadString(element, "source") ?? ReadString(element, "sourceName") ?? "TradingView",
            Url = url,
            PublishedAt = published
        };
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateTimeOffset? ReadDate(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix);

        return property.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(property.GetString(), out var parsed)
            ? parsed
            : null;
    }
}
