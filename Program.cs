using Microsoft.Extensions.Caching.Memory;
using StatusImageCard.Service;
using System.Text.Json;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ContainerRotationService>();

builder.Services.AddHttpClient("zcog", c =>
{
    var baseUrl = builder.Configuration["BASE_URL"] ?? throw new Exception("Missing BASE_URL");
    c.BaseAddress = new Uri(baseUrl);
    c.Timeout = TimeSpan.FromSeconds(3);
});

var jsonOpts = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

var app = builder.Build();

app.MapGet("/status.png", async (HttpContext http, IHttpClientFactory httpClientFactory, IMemoryCache cache, ContainerRotationService rotation, CancellationToken ct) =>
{
    // Hours stays user-configurable
    int hours = 0;
    if (http.Request.Query.TryGetValue("h", out var hv) && int.TryParse(hv.ToString(), out var hParsed)) hours = hParsed;
    else if (int.TryParse(Environment.GetEnvironmentVariable("DEFAULT_HOURS"), out var hEnv)) hours = hEnv;
    else hours = 24;

    // Rotation cache window (fixed 60s per your requirement)
    const int cacheSeconds = 60;

    // If container query is provided, do "pinned" rendering. Otherwise rotate.
    bool pinned = http.Request.Query.TryGetValue("container", out var cv) && !string.IsNullOrWhiteSpace(cv.ToString());
    string? containerSelector = pinned ? cv.ToString() : null;

    // Cache key: one key for rotating, or per-container for pinned
    string cacheKey = pinned
        ? $"status-png:pinned:{containerSelector}:{hours}"
        : $"status-png:rotating:{hours}";

    if (!cache.TryGetValue(cacheKey, out byte[]? png) || png == null)
    {
        // Lock so only one request generates when cache expires
        png = await rotation.GeneratePngAsync(cacheKey, cacheSeconds, cache, httpClientFactory, jsonOpts, hours, containerSelector, ct);
    }

    http.Response.Headers.CacheControl = $"public,max-age={cacheSeconds}";
    return Results.File(png, "image/png");
});

app.Run();
