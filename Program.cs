using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using StatusImageCard.Api;
using StatusImageCard.Config;
using StatusImageCard.Discord;
using StatusImageCard.Helper;
using StatusImageCard.Service;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

RequireHelper.Require(builder.Configuration, EnvKeys.BaseUrl);
RequireHelper.Require(builder.Configuration, EnvKeys.ContainerWhitelist);
RequireHelper.Require(builder.Configuration, EnvKeys.DiscordWebhookUrl);

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<ContainerRotationService>();
builder.Services.AddSingleton<IContainerRotationService>(sp => sp.GetRequiredService<ContainerRotationService>());
builder.Services.AddSingleton<IInternalApiService, InternalApiService>();

builder.Services.AddHttpClient<DiscordWebhookClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHostedService<DiscordStatusUpdaterService>();

builder.Services.AddHttpClient("api", c =>
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

app.MapGet("/status.png", async (
    HttpContext http,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ContainerRotationService rotation,
    JsonSerializerOptions jsonOptions,
    ILogger<Program> log,
    CancellationToken ct) =>
{
    const int hours = 6;
    const int cacheSeconds = 60 * 5;

    try
    {
        bool pinned = http.Request.Query.TryGetValue("container", out var cv) &&
                      !string.IsNullOrWhiteSpace(cv.ToString());
        string? containerSelector = pinned ? cv.ToString() : null;

        string cacheKey = pinned
            ? $"status-png:pinned:{containerSelector}:{hours}"
            : $"status-png:rotating:{hours}";

        // keep endpoint behavior: still uses cached png bytes
        var png = await rotation.GenerateStatusAsync(
            cacheKey: cacheKey,
            cacheSeconds: cacheSeconds,
            cache: cache,
            httpClientFactory: httpClientFactory,
            jsonOpts: jsonOptions,
            hours: hours,
            pinnedContainerSelector: containerSelector,
            ct: ct);

        http.Response.Headers.CacheControl = $"public,max-age={cacheSeconds}";
        return Results.File(png.PngBytes, "image/png");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to generate /status.png");
        var fallback = StatusCardRenderService.RenderAllOffline(width: 640, height: 420);
        return Results.File(fallback, "image/png");
    }
});

app.Run();
