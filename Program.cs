using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using StatusImageCard.Api;
using StatusImageCard.Config;
using StatusImageCard.Discord;
using StatusImageCard.Health;
using StatusImageCard.Helper;
using StatusImageCard.Service;

// `dotnet StatusImageCard.dll healthcheck`: probe the running instance and exit, no web host started.
if (args is [HealthCheckCommand.Arg])
{
    var url = HealthCheckCommand.DefaultUrl(Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS"));
    return await HealthCheckCommand.RunAsync(url, TimeSpan.FromSeconds(5));
}

// Loads a local .env for development runs; a missing file is ignored (the image ships without one).
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

RequireHelper.Require(builder.Configuration, EnvKeys.BaseUrl);
RequireHelper.Require(builder.Configuration, EnvKeys.ContainerWhitelist);
RequireHelper.Require(builder.Configuration, EnvKeys.DiscordWebhookUrl);

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<IInternalApiService, InternalApiService>();
builder.Services.AddSingleton<IStatusImageService, StatusImageService>();

builder.Services.AddHttpClient<DiscordWebhookClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHostedService<DiscordStatusUpdaterService>();

builder.Services.AddHttpClient(InternalApiService.ClientName, c =>
{
    var baseUrl = builder.Configuration[EnvKeys.BaseUrl];
    if (string.IsNullOrEmpty(baseUrl))
        throw new Exception("BASE_URL configuration is required for API client.");
    c.BaseAddress = new Uri(baseUrl);
    c.Timeout = TimeSpan.FromSeconds(30);
});

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
builder.Services.AddSingleton(jsonOpts);

var app = builder.Build();

// Liveness only: no monitor call, so it stays cheap and answers even when the monitor is down.
app.MapGet(HealthCheckCommand.Path, () => Results.Text("Healthy"));

// GET /status.png             -> grid of every whitelisted server (cached briefly)
// GET /status.png?container=x -> one card for the whitelisted container whose name or id prefix is x (404 otherwise)
app.MapGet("/status.png", async (
    HttpContext http,
    IStatusImageService statusImages,
    IMemoryCache cache,
    ILogger<Program> log,
    CancellationToken ct) =>
{
    const int cacheSeconds = 60;
    const string gridCacheKey = "status-png:grid";

    string? selector = http.Request.Query["container"].ToString().Trim();
    if (string.IsNullOrEmpty(selector))
        selector = null;

    try
    {
        byte[] png;
        if (selector is null)
        {
            if (!cache.TryGetValue(gridCacheKey, out byte[]? cached) || cached is null)
            {
                var result = await statusImages.RenderGridAsync(ct);
                cached = result.PngBytes;
                if (!result.IsFallback)
                    cache.Set(gridCacheKey, cached, TimeSpan.FromSeconds(cacheSeconds));
            }
            png = cached;
        }
        else
        {
            png = await statusImages.RenderSingleAsync(selector, ct);
        }

        http.Response.Headers.CacheControl = $"public,max-age={cacheSeconds}";
        return Results.File(png, "image/png");
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (Exception ex) when (!ct.IsCancellationRequested)
    {
        log.LogError(ex, "Failed to generate /status.png");
        var fallback = StatusCardRenderService.RenderAllOffline(StatusCardRenderService.TileWidth, StatusCardRenderService.TileHeight);
        return Results.File(fallback, "image/png");
    }
});

app.Run();
return 0;
