using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using StatusImageCard.Api;
using StatusImageCard.Config;
using StatusImageCard.Service;
using StatusImageCard.Helper;
using StatusImageCard.Discord;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

RequireHelper.Require(builder.Configuration, EnvKeys.BaseUrl);
RequireHelper.Require(builder.Configuration, EnvKeys.ContainerWhitelist);
RequireHelper.Require(builder.Configuration, EnvKeys.DiscordWebhookUrl);
RequireHelper.Require(builder.Configuration, EnvKeys.StatusImageUrl);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ContainerRotationService>();
builder.Services.AddSingleton<IInternalApiService, InternalApiService>();

// Typed Discord client (used by DiscordStatusUpdaterService)
builder.Services.AddHttpClient<DiscordWebhookClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});

// Needed so DiscordStatusUpdaterService can fetch the PNG bytes from STATUS_IMAGE_URL
builder.Services.AddHttpClient("image-fetch", c =>
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
    c.Timeout = TimeSpan.FromSeconds(3);
});

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
builder.Services.AddSingleton(jsonOpts);

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var v = app.Configuration[EnvKeys.StatusImageUrl];
    app.Logger.LogInformation("STATUS_IMAGE_URL resolved to: {Value}", v);
});

app.MapGet("/status.png", async (
    HttpContext http,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ContainerRotationService rotation,
    CancellationToken ct) =>
{
    const int hours = 6;
    const int cacheSeconds = 60;

    bool pinned = http.Request.Query.TryGetValue("container", out var cv) && !string.IsNullOrWhiteSpace(cv.ToString());
    string? containerSelector = pinned ? cv.ToString() : null;

    string cacheKey = pinned
        ? $"status-png:pinned:{containerSelector}:{hours}"
        : $"status-png:rotating:{hours}";

    if (!cache.TryGetValue(cacheKey, out byte[]? png) || png == null)
        png = await rotation.GeneratePngAsync(cacheKey, cacheSeconds, cache, httpClientFactory, jsonOpts, hours, containerSelector, ct);

    http.Response.Headers.CacheControl = $"public,max-age={cacheSeconds}";
    return Results.File(png, "image/png");
});

app.Run();
