using StockOpportunityAutomation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ScannerOptions>(builder.Configuration.GetSection("Scanner"));
builder.Services.Configure<NewsOptions>(builder.Configuration.GetSection("News"));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<AutomationOptions>(builder.Configuration.GetSection("Automation"));
builder.Services.Configure<PaperTradingOptions>(builder.Configuration.GetSection("PaperTrading"));

builder.Services.AddHttpClient<IScannerClient, ScannerClient>();
builder.Services.AddHttpClient<IStockNewsClient, TradingViewNewsClient>();
builder.Services.AddHttpClient<IOpenAiDecisionClient, OpenAiDecisionClient>();
builder.Services.AddScoped<IOpportunityEngine, OpportunityEngine>();
builder.Services.AddHostedService<OpportunityWorker>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/info", () => Results.Ok(new
{
    service = "Stock Opportunity Automation",
    status = "running",
    ordersEnabled = false,
    paperTrading = true,
    maxCapitalPerTrade = 20000
}));

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));

app.MapGet("/api/opportunities", async (
    int? limit,
    IOpportunityEngine engine,
    CancellationToken cancellationToken) =>
{
    var result = await engine.ScanAsync(limit, cancellationToken);
    return Results.Ok(result);
});

app.Run();
