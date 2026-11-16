namespace StatusImageCard.Health;

/// <summary>
/// `dotnet StatusImageCard.dll healthcheck`: GETs the running app's /health on loopback and exits 0 (healthy) or 1.
/// Lets the container healthcheck itself without curl or any other package in the image.
/// </summary>
public static class HealthCheckCommand
{
    public const string Arg = "healthcheck";
    public const string Path = "/health";

    /// <summary>The URL the running app's /health is on: the first port in ASPNETCORE_HTTP_PORTS, else .NET's default 8080.</summary>
    public static Uri DefaultUrl(string? httpPorts)
    {
        var first = (httpPorts ?? "")
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        int port = int.TryParse(first, out var p) && p is > 0 and <= 65535 ? p : 8080;
        return new Uri($"http://127.0.0.1:{port}{Path}");
    }

    public static async Task<int> RunAsync(Uri url, TimeSpan timeout)
    {
        try
        {
            using var http = new HttpClient { Timeout = timeout };
            using var res = await http.GetAsync(url);
            return res.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
