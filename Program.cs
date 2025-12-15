using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp;
using SkiaSharp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();

// Configure this to your internal monitor service address:
builder.Services.AddHttpClient("monitor", c =>
{
  // Example: http://norsworthy-monitor:8080  (docker compose service name)
  c.BaseAddress = new Uri(builder.Configuration["Monitor:BaseUrl"] ?? "http://localhost:5001");
  c.Timeout = TimeSpan.FromSeconds(2);
});

var app = builder.Build();

app.MapGet("/status.png", async (
    HttpContext http,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    CancellationToken ct) =>
{
  bool demo = http.Request.Query.TryGetValue("demo", out var dv) && dv.ToString() == "1";

  // Small cache to avoid hammering your monitor API if the image is refreshed frequently.
  // Include demo flag in cache key.
  string cacheKey = $"status-png:{(demo ? "demo" : "live")}";

  if (!cache.TryGetValue(cacheKey, out byte[]? png) || png == null)
  {
    MonitorStatus status;

    if (demo)
    {
      status = MonitorStatus.CreateDemo();
    }
    else
    {
      status = await FetchMonitorStatusOrFallback(httpClientFactory, ct);
    }

    png = StatusCardRenderer.Render(status, width: 900, height: 500);

    cache.Set(cacheKey, png, new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
    });
  }

  // Optional: allow external caches/CDNs to store briefly. Adjust to your needs.
  http.Response.Headers.CacheControl = "public,max-age=5";
  http.Response.Headers.ETag = $"W/\"{png.Length}-{(demo ? "demo" : "live")}\"";

  return Results.File(png, "image/png");
});

app.Run();

static async Task<MonitorStatus> FetchMonitorStatusOrFallback(IHttpClientFactory factory, CancellationToken ct)
{
  try
  {
    var client = factory.CreateClient("monitor");

    // Change path to match your real internal API
    var resp = await client.GetAsync("/api/status", ct);

    if (resp.StatusCode == HttpStatusCode.NotFound)
      return MonitorStatus.CreateDemo(); // helps you see something if endpoint isn't ready yet

    resp.EnsureSuccessStatusCode();

    var status = await resp.Content.ReadFromJsonAsync<MonitorStatus>(cancellationToken: ct);
    return status ?? MonitorStatus.CreateDemo();
  }
  catch
  {
    // If the internal monitor is down/slow, you can either:
    //  - return a “degraded” card (recommended)
    //  - or return 503
    var s = MonitorStatus.CreateDemo();
    s.HealthMessage = "Monitor unreachable (showing demo/fallback)";
    s.HealthState = HealthState.Degraded;
    return s;
  }
}

public enum HealthState { Healthy, Degraded, Down }

public sealed class MonitorStatus
{
  public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

  public HealthState HealthState { get; set; } = HealthState.Healthy;
  public string HealthMessage { get; set; } = "OK";

  public int TotalContainers { get; set; }
  public int RunningContainers { get; set; }
  public int StoppedContainers { get; set; }

  // Example “chart series”: CPU percent points over time
  public double[] CpuTrend { get; set; } = Array.Empty<double>();

  public static MonitorStatus CreateDemo()
  {
    var rnd = new Random(1234);

    int total = 12;
    int running = 10;
    int stopped = total - running;

    // Create trend data
    var points = new double[40];
    double v = 18;
    for (int i = 0; i < points.Length; i++)
    {
      v += (rnd.NextDouble() - 0.45) * 4.0;          // wobble
      v = Math.Clamp(v, 5, 75);                     // keep it reasonable
      points[i] = Math.Round(v, 1);
    }

    return new MonitorStatus
    {
      Timestamp = DateTimeOffset.UtcNow,
      HealthState = HealthState.Healthy,
      HealthMessage = "All systems nominal",
      TotalContainers = total,
      RunningContainers = running,
      StoppedContainers = stopped,
      CpuTrend = points
    };
  }
}

public static class StatusCardRenderer
{
  public static byte[] Render(MonitorStatus s, int width, int height)
  {
    using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(new SKColor(245, 246, 248));

    // Title
    using var titleFont = new SKFont { Size = 30 };
    using var titlePaint = new SKPaint { IsAntialias = true, Color = new SKColor(25, 25, 25) };
    canvas.DrawText("Norsworthy Monitor", 40, 58, SKTextAlign.Left, titleFont, titlePaint);

    // Subheader
    using var subFont = new SKFont { Size = 16 };
    using var subPaint = new SKPaint { IsAntialias = true, Color = new SKColor(90, 90, 90) };
    canvas.DrawText($"Updated: {s.Timestamp:yyyy-MM-dd HH:mm:ss} UTC", 40, 82, SKTextAlign.Left, subFont, subPaint);

    // Health pill
    var healthRect = new SKRect(650, 28, 860, 74);
    var (healthFill, healthText) = s.HealthState switch
    {
      HealthState.Healthy => (new SKColor(232, 245, 233), "Healthy"),
      HealthState.Degraded => (new SKColor(255, 243, 224), "Degraded"),
      _ => (new SKColor(255, 235, 238), "Down")
    };
    DrawRoundedCard(canvas, healthRect, 18, healthFill);
    using (var hf = new SKFont { Size = 16 })
    using (var hp = new SKPaint { IsAntialias = true, Color = new SKColor(25, 25, 25) })
      canvas.DrawText(healthText, healthRect.Left + 18, healthRect.Top + 30, SKTextAlign.Left, hf, hp);

    // Stat pills
    DrawPill(canvas, new SKRect(40, 95, 320, 170), new SKColor(227, 242, 253), "Total", s.TotalContainers.ToString());
    DrawPill(canvas, new SKRect(340, 95, 620, 170), new SKColor(232, 245, 233), "Running", s.RunningContainers.ToString());
    DrawPill(canvas, new SKRect(640, 95, 860, 170), new SKColor(255, 243, 224), "Stopped", s.StoppedContainers.ToString());

    // Chart card
    var chartCard = new SKRect(40, 200, width - 40, height - 40);
    DrawRoundedCard(canvas, chartCard, 18, SKColors.White);

    using var chartTitleFont = new SKFont { Size = 18 };
    using var chartTitle = new SKPaint { IsAntialias = true, Color = new SKColor(30, 30, 30) };
    canvas.DrawText("CPU Trend (%)", chartCard.Left + 20, chartCard.Top + 32, SKTextAlign.Left, chartTitleFont, chartTitle);

    // Optional message line
    using var msgFont = new SKFont { Size = 14 };
    using var msgPaint = new SKPaint { IsAntialias = true, Color = new SKColor(90, 90, 90) };
    canvas.DrawText(s.HealthMessage, chartCard.Left + 20, chartCard.Top + 54, SKTextAlign.Left, msgFont, msgPaint);

    // Render chart via OxyPlot (transparent background) and composite onto the card
    var plotArea = new SKRect(chartCard.Left + 20, chartCard.Top + 70, chartCard.Right - 20, chartCard.Bottom - 20);
    var plot = OxyChart.BuildCpuPlot(s.CpuTrend);
    var chartPng = OxyChart.RenderPlotPng(plot, (int)plotArea.Width, (int)plotArea.Height);

    using (var chartBitmap = SKBitmap.Decode(chartPng))
    {
      canvas.DrawBitmap(chartBitmap, plotArea);
    }

    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
  }

  private static void DrawPill(SKCanvas canvas, SKRect rect, SKColor fill, string label, string value)
  {
    DrawRoundedCard(canvas, rect, 16, fill);

    using var labelFont = new SKFont { Size = 15 };
    using var labelPaint = new SKPaint { IsAntialias = true, Color = new SKColor(80, 80, 80) };
    using var valueFont = new SKFont { Size = 26 };
    using var valuePaint = new SKPaint { IsAntialias = true, Color = new SKColor(25, 25, 25) };

    canvas.DrawText(label, rect.Left + 16, rect.Top + 32, SKTextAlign.Left, labelFont, labelPaint);
    canvas.DrawText(value, rect.Left + 16, rect.Top + 68, SKTextAlign.Left, valueFont, valuePaint);
  }

  private static void DrawRoundedCard(SKCanvas canvas, SKRect rect, float radius, SKColor fill)
  {
    using var paint = new SKPaint { IsAntialias = true, Color = fill, Style = SKPaintStyle.Fill };
    canvas.DrawRoundRect(rect, radius, radius, paint);

    using var border = new SKPaint { IsAntialias = true, Color = new SKColor(225, 225, 225), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    canvas.DrawRoundRect(rect, radius, radius, border);
  }
}

public static class OxyChart
{
  public static PlotModel BuildCpuPlot(double[] points)
  {
    var model = new PlotModel();

    // Transparent so it blends into the SkiaSharp card nicely
    model.Background = OxyColors.Transparent;
    model.PlotAreaBorderColor = OxyColor.FromRgb(220, 220, 220);

    // Axes
    model.Axes.Add(new LinearAxis
    {
      Position = AxisPosition.Left,
      Title = "%",
      MinimumPadding = 0.1,
      MaximumPadding = 0.1,
      MajorGridlineStyle = LineStyle.Solid,
      MinorGridlineStyle = LineStyle.None,
      MajorGridlineColor = OxyColor.FromRgb(235, 235, 235)
    });

    model.Axes.Add(new LinearAxis
    {
      Position = AxisPosition.Bottom,
      IsAxisVisible = false,
      MajorGridlineStyle = LineStyle.Solid,
      MajorGridlineColor = OxyColor.FromRgb(235, 235, 235)
    });

    var series = new LineSeries
    {
      StrokeThickness = 2,
      MarkerType = MarkerType.Circle,
      MarkerSize = 3
    };

    if (points == null || points.Length < 2)
      points = new double[] { 10, 12, 9, 14, 13, 16 };

    for (int i = 0; i < points.Length; i++)
      series.Points.Add(new DataPoint(i, points[i]));

    model.Series.Add(series);
    return model;
  }

  public static byte[] RenderPlotPng(PlotModel model, int width, int height)
  {
    using var ms = new MemoryStream();

    var exporter = new PngExporter
    {
      Width = Math.Max(width, 1),
      Height = Math.Max(height, 1)
    };

    exporter.Export(model, ms);
    return ms.ToArray();
  }
}
