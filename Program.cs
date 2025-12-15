using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp;
using SkiaSharp;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddMemoryCache();

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

app.MapGet("/status.png", async (HttpContext http, IHttpClientFactory httpClientFactory, IMemoryCache cache, CancellationToken ct) =>
{
    string container = http.Request.Query.TryGetValue("container", out var cv) ? cv.ToString() : (Environment.GetEnvironmentVariable("DEFAULT_CONTAINER") ?? "minecraft");

    int hours = 0;
    if (http.Request.Query.TryGetValue("h", out var hv) && int.TryParse(hv.ToString(), out var hParsed)) hours = hParsed;
    else if (int.TryParse(Environment.GetEnvironmentVariable("DEFAULT_HOURS"), out var hEnv)) hours = hEnv;
    else hours = 24;

    int cacheSeconds = int.TryParse(Environment.GetEnvironmentVariable("CACHE_SECONDS"), out var cs) ? cs : 5;

    string cacheKey = $"status-png:{container}:{hours}";

    if (!cache.TryGetValue(cacheKey, out byte[]? png) || png == null)
    {
        ContainerCard card = await FetchFromApi(httpClientFactory, container, hours, jsonOpts, ct);

        png = StatusCardRenderer.Render(card, width: 640, height: 420);

        cache.Set(cacheKey, png, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSeconds)
        });
    }

    http.Response.Headers.CacheControl = $"public,max-age={cacheSeconds}";
    return Results.File(png, "image/png");
});

app.Run();

static async Task<ContainerCard> FetchFromApi(IHttpClientFactory factory, string containerSelector, int hours, JsonSerializerOptions jsonOpts, CancellationToken ct)
{
    try
    {
        var client = factory.CreateClient("zcog");

        // 1) current snapshot list
        var listResp = await client.GetAsync("/api/containers/stats/", ct);
        listResp.EnsureSuccessStatusCode();

        var list = await listResp.Content.ReadFromJsonAsync<List<ZcogContainerStat>>(jsonOpts, ct) ?? new List<ZcogContainerStat>();

        // Find by exact name, or by id prefix, or full id
        var current = list.FirstOrDefault(x =>
            x.Name.Equals(containerSelector, StringComparison.OrdinalIgnoreCase) ||
            x.Id.StartsWith(containerSelector, StringComparison.OrdinalIgnoreCase));

        if (current == null)
        {
            throw new Exception("Current container not found");
        }

        // 2) history for that container
        var histUrl = $"/api/containers/stats/{current.Id}?h={hours}";
        var histResp = await client.GetAsync(histUrl, ct);
        histResp.EnsureSuccessStatusCode();

        var hist = await histResp.Content.ReadFromJsonAsync<List<ZcogContainerStat>>(jsonOpts, ct) ?? new List<ZcogContainerStat>();

        return MapToCard(current, hist, hours);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Failed to fetch");
        throw;
    }
}

static ContainerCard MapToCard(ZcogContainerStat current, List<ZcogContainerStat> history, int hours)
{
    // memory values from your API appear to be MB; convert to GB for display
    double usedGb = current.MemoryCurrent / 1024.0;
    double maxGb = current.MemoryMax / 1024.0;

    var state = current.State?.Equals("running", StringComparison.OrdinalIgnoreCase) == true
        ? StatusState.Online
        : StatusState.Offline;
    Console.WriteLine(current);

    string statusText = state == StatusState.Online ? "Online" : "Offline";

    // status string already contains the "Up ..." text in your payload
    string uptime = ExtractUpPrefix(current.Status) ?? (state == StatusState.Online ? "Up" : "Down");
    string health = ExtractHealthSuffix(current.Status) ?? (state == StatusState.Online ? "healthy" : "unhealthy");

    // Build chart samples from history
    var usage = new List<UsageSample>(history.Count);
    foreach (var h in history)
    {
        var ts = ParseZcogTimestamp(h.Timestamp);
        usage.Add(new UsageSample
        {
            TsUtc = ts,
            CpuPercent = h.CpuPercent,
            MemoryPercent = h.MemoryPercent
        });
    }

    return new ContainerCard
    {
        Name = current.Name,
        IdShort = current.Id.Length >= 12 ? current.Id[..12] + "..." : current.Id,
        StatusState = state,
        StatusText = statusText,
        UptimeText = uptime,
        HealthText = health,
        CpuNowPercent = current.CpuPercent,
        MemoryUsedGb = Math.Round(usedGb, 2),
        MemoryTotalGb = Math.Round(maxGb, 2),
        Subtitle = $"Usage History (Last {hours} hour{(hours == 1 ? "" : "s")})",
        UsageLastDay = usage
    };
}

static DateTimeOffset ParseZcogTimestamp(string ts)
{
    // Example: "2025-12-15T15:34:08" (no timezone)
    // Treat as UTC to keep your charts stable.
    if (DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
    {
        if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return new DateTimeOffset(dt);
    }

    return DateTimeOffset.UtcNow;
}

static string? ExtractUpPrefix(string status)
{
    // "Up 26 hours (healthy)" -> "Up 26 hours"
    int idx = status.IndexOf("(", StringComparison.Ordinal);
    return idx > 0 ? status[..idx].Trim() : status;
}

static string? ExtractHealthSuffix(string status)
{
    // "Up 26 hours (healthy)" -> "healthy"
    int a = status.IndexOf("(", StringComparison.Ordinal);
    int b = status.IndexOf(")", StringComparison.Ordinal);
    if (a >= 0 && b > a) return status[(a + 1)..b].Trim();
    return null;
}

/* =======================
   DTOs matching your API
   ======================= */

public sealed class ZcogContainerStat
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("cpu_current")] public double CpuCurrent { get; set; }
    [JsonPropertyName("cpu_max")] public double CpuMax { get; set; }
    [JsonPropertyName("cpu_percent")] public double CpuPercent { get; set; }

    [JsonPropertyName("memory_current")] public double MemoryCurrent { get; set; } // MB
    [JsonPropertyName("memory_max")] public double MemoryMax { get; set; }         // MB
    [JsonPropertyName("memory_percent")] public double MemoryPercent { get; set; }

    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";

    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
}

/* =======================
   Rendering models
   ======================= */

public enum StatusState { Online, Offline }

public sealed class UsageSample
{
    public DateTimeOffset TsUtc { get; set; }
    public double CpuPercent { get; set; }
    public double MemoryPercent { get; set; }
}

public sealed class ContainerCard
{
    public required string Name { get; set; }
    public required string IdShort { get; set; }
    public required StatusState StatusState { get; set; }
    public required string StatusText { get; set; }
    public required string UptimeText { get; set; }
    public required string HealthText { get; set; }
    public required double CpuNowPercent { get; set; }
    public required double MemoryUsedGb { get; set; }
    public required double MemoryTotalGb { get; set; }
    public required string Subtitle { get; set; }
    public required List<UsageSample> UsageLastDay { get; set; }
}

/* =======================
   Dark-card renderer
   ======================= */

public static class StatusCardRenderer
{
    private static readonly SKColor PageBg = SKColor.Parse("#0b0d10");
    private static readonly SKColor CardBg = SKColor.Parse("#14161a");
    private static readonly SKColor CardBorder = SKColor.Parse("#262a31");
    private static readonly SKColor MutedText = SKColor.Parse("#a6adbb");
    private static readonly SKColor MainText = SKColor.Parse("#f2f4f8");
    private static readonly SKColor Divider = SKColor.Parse("#232730");

    private static readonly SKColor OnlinePill = SKColor.Parse("#22c55e");
    private static readonly SKColor OnlineText = SKColor.Parse("#0b0d10");
    private static readonly SKColor OfflinePill = SKColor.Parse("#2b2f37");
    private static readonly SKColor OfflineText = MainText;

    private static readonly SKColor CpuBlue = SKColor.Parse("#3b82f6");
    private static readonly SKColor MemGreen = SKColor.Parse("#22c55e");

    public static byte[] Render(ContainerCard c, int width, int height)
    {
        using var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);

        canvas.Clear(PageBg);

        float pad = 18;
        var cardRect = new SKRect(pad, pad, width - pad, height - pad);

        DrawShadowedCard(canvas, cardRect, 18);

        float x0 = cardRect.Left + 18;
        float y0 = cardRect.Top + 16;

        var iconRect = new SKRect(x0, y0, x0 + 34, y0 + 34);
        DrawIconBadge(canvas, iconRect);

        float textLeft = iconRect.Right + 12;

        using var nameFont = new SKFont { Size = 20 };
        using var namePaint = new SKPaint { IsAntialias = true, Color = MainText };
        canvas.DrawText(c.Name, textLeft, y0 + 22, SKTextAlign.Left, nameFont, namePaint);

        using var idFont = new SKFont { Size = 13 };
        using var idPaint = new SKPaint { IsAntialias = true, Color = MutedText };
        canvas.DrawText(c.IdShort, textLeft, y0 + 40, SKTextAlign.Left, idFont, idPaint);

        string pillText = c.StatusText;
        var (pillFill, pillTextColor) = c.StatusState == StatusState.Online
            ? (OnlinePill, OnlineText)
            : (OfflinePill, OfflineText);

        using var pillFont = new SKFont { Size = 13 };
        float pillPadX = 14, pillPadY = 8;
        float pillW = pillFont.MeasureText(pillText) + pillPadX * 2;
        float pillH = pillFont.Size + pillPadY * 2;

        var pillRect = new SKRect(cardRect.Right - 18 - pillW, y0 + 4, cardRect.Right - 18, y0 + 4 + pillH);
        DrawRoundedRect(canvas, pillRect, pillH / 2f, pillFill, border: null);

        using var pillPaint = new SKPaint { IsAntialias = true, Color = pillTextColor };
        canvas.DrawText(pillText, pillRect.Left + pillPadX, pillRect.MidY + (pillFont.Size * 0.35f),
            SKTextAlign.Left, pillFont, pillPaint);

        using var upFont = new SKFont { Size = 13 };
        using var upPaint = new SKPaint { IsAntialias = true, Color = MutedText };
        canvas.DrawText($"{c.UptimeText} ({c.HealthText})", x0, y0 + 72, SKTextAlign.Left, upFont, upPaint);

        float metricsY = y0 + 104;

        DrawInlineLabelValue(canvas, x0, metricsY, "CPU: ", $"{c.CpuNowPercent:0.##}%", MutedText, MainText);

        string memValue = $"{c.MemoryUsedGb:0.##}GB / {c.MemoryTotalGb:0.##}GB";
        DrawInlineLabelValue(canvas, cardRect.MidX + 20, metricsY, "Memory: ", memValue, MutedText, MainText);

        float divY = metricsY + 18;
        using (var divPaint = new SKPaint { IsAntialias = true, Color = Divider, StrokeWidth = 1 })
            canvas.DrawLine(cardRect.Left + 16, divY, cardRect.Right - 16, divY, divPaint);

        using var chartTitleFont = new SKFont { Size = 14 };
        using var chartTitlePaint = new SKPaint { IsAntialias = true, Color = MainText };
        canvas.DrawText("Usage History", cardRect.MidX, divY + 28, SKTextAlign.Center, chartTitleFont, chartTitlePaint);

        float legendY = divY + 52;
        DrawLegendItem(canvas, cardRect.MidX - 60, legendY, CpuBlue, "CPU %");
        DrawLegendItem(canvas, cardRect.MidX + 10, legendY, MemGreen, "Memory %");

        if (!string.IsNullOrWhiteSpace(c.Subtitle))
        {
            using var subFont = new SKFont { Size = 12 };
            using var subPaint = new SKPaint { IsAntialias = true, Color = MutedText };
            canvas.DrawText(c.Subtitle, x0, legendY + 22, SKTextAlign.Left, subFont, subPaint);
        }

        var plotRect = new SKRect(cardRect.Left + 50, legendY + 16, cardRect.Right - 24, cardRect.Bottom - 22);

        var plot = OxyChart.BuildUsagePlot(c.UsageLastDay);
        var chartPng = OxyChart.RenderPlotPng(plot, (int)plotRect.Width, (int)plotRect.Height);

        using (var chartBmp = SKBitmap.Decode(chartPng))
            canvas.DrawBitmap(chartBmp, plotRect);

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawShadowedCard(SKCanvas canvas, SKRect rect, float radius)
    {
        using (var shadow = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0, 0, 0, 140),
            ImageFilter = SKImageFilter.CreateBlur(10, 10)
        })
        {
            var shadowRect = rect;
            shadowRect.Offset(0, 6);
            canvas.DrawRoundRect(shadowRect, radius, radius, shadow);
        }

        DrawRoundedRect(canvas, rect, radius, CardBg, CardBorder);
    }

    private static void DrawRoundedRect(SKCanvas canvas, SKRect rect, float radius, SKColor fill, SKColor? border)
    {
        using var fillPaint = new SKPaint { IsAntialias = true, Color = fill, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(rect, radius, radius, fillPaint);

        if (border.HasValue)
        {
            using var borderPaint = new SKPaint { IsAntialias = true, Color = border.Value, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            canvas.DrawRoundRect(rect, radius, radius, borderPaint);
        }
    }

    private static void DrawIconBadge(SKCanvas canvas, SKRect rect)
    {
        DrawRoundedRect(canvas, rect, 10, SKColor.Parse("#1f232b"), border: SKColor.Parse("#2b303a"));

        using var p = new SKPaint { IsAntialias = true, Color = MainText, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f };

        float cx = rect.MidX, cy = rect.MidY;
        float s = rect.Width * 0.22f;

        var f = new SKRect(cx - s, cy - s, cx + s, cy + s);
        canvas.DrawRect(f, p);

        float o = s * 0.7f;
        var b = new SKRect(f.Left - o, f.Top - o, f.Right - o, f.Bottom - o);
        canvas.DrawRect(b, p);

        canvas.DrawLine(f.Left, f.Top, b.Left, b.Top, p);
        canvas.DrawLine(f.Right, f.Top, b.Right, b.Top, p);
        canvas.DrawLine(f.Left, f.Bottom, b.Left, b.Bottom, p);
        canvas.DrawLine(f.Right, f.Bottom, b.Right, b.Bottom, p);
    }

    private static void DrawInlineLabelValue(SKCanvas canvas, float x, float y, string label, string value, SKColor labelColor, SKColor valueColor)
    {
        using var font = new SKFont { Size = 13 };
        using var labelPaint = new SKPaint { IsAntialias = true, Color = labelColor };
        using var valuePaint = new SKPaint { IsAntialias = true, Color = valueColor };

        canvas.DrawText(label, x, y, SKTextAlign.Left, font, labelPaint);
        float labelW = font.MeasureText(label);
        canvas.DrawText(value, x + labelW, y, SKTextAlign.Left, font, valuePaint);
    }

    private static void DrawLegendItem(SKCanvas canvas, float x, float y, SKColor color, string text)
    {
        using var dotPaint = new SKPaint { IsAntialias = true, Color = color, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(x, y - 4, 3.5f, dotPaint);

        using var f = new SKFont { Size = 12 };
        using var p = new SKPaint { IsAntialias = true, Color = MainText };
        canvas.DrawText(text, x + 10, y, SKTextAlign.Left, f, p);
    }
}

public static class OxyChart
{
    private static readonly OxyColor CpuBlue = OxyColor.FromRgb(59, 130, 246);
    private static readonly OxyColor MemGreen = OxyColor.FromRgb(34, 197, 94);

    public static PlotModel BuildUsagePlot(List<UsageSample> samples)
    {
        var model = new PlotModel
        {
            Background = OxyColors.Transparent,
            PlotAreaBackground = OxyColors.Transparent,
            IsLegendVisible = false,
            TextColor = OxyColor.FromRgb(242, 244, 248),
            DefaultFontSize = 11
        };

        var grid = OxyColor.FromRgb(35, 39, 48);
        var axis = OxyColor.FromRgb(90, 98, 112);

        model.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "yyyy-MM-ddTHH:mm:ss",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = grid,
            MinorGridlineStyle = LineStyle.None,
            AxislineColor = axis,
            TicklineColor = axis,
            TextColor = OxyColor.FromRgb(166, 173, 187)
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = 0,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = grid,
            MinorGridlineStyle = LineStyle.None,
            AxislineColor = axis,
            TicklineColor = axis,
            TextColor = OxyColor.FromRgb(166, 173, 187)
        });

        var cpu = new LineSeries { Color = CpuBlue, StrokeThickness = 2, MarkerType = MarkerType.None };
        var mem = new LineSeries { Color = MemGreen, StrokeThickness = 2, MarkerType = MarkerType.None };

        foreach (var s in samples.OrderBy(x => x.TsUtc))
        {
            var t = DateTimeAxis.ToDouble(s.TsUtc.UtcDateTime);
            cpu.Points.Add(new DataPoint(t, s.CpuPercent));
            mem.Points.Add(new DataPoint(t, s.MemoryPercent));
        }

        model.Series.Add(cpu);
        model.Series.Add(mem);
        return model;
    }

    public static byte[] RenderPlotPng(PlotModel model, int width, int height)
    {
        using var ms = new MemoryStream();
        new PngExporter
        {
            Width = Math.Max(width, 1),
            Height = Math.Max(height, 1)
        }.Export(model, ms);
        return ms.ToArray();
    }
}
