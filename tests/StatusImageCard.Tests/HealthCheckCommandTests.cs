using System.Net;
using System.Net.Sockets;
using StatusImageCard.Health;

namespace StatusImageCard.Tests;

public class HealthCheckCommandTests
{
    [Theory]
    [InlineData(null, 8080)]
    [InlineData("", 8080)]
    [InlineData("9000", 9000)]
    [InlineData(" 9000 ; 9001", 9000)]
    [InlineData("nope", 8080)]
    [InlineData("70000", 8080)]
    public void DefaultUrl_uses_the_first_http_port_or_8080(string? httpPorts, int expectedPort)
    {
        Assert.Equal(new Uri($"http://127.0.0.1:{expectedPort}/health"), HealthCheckCommand.DefaultUrl(httpPorts));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, 0)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 1)]
    public async Task Exit_code_follows_the_health_response(HttpStatusCode status, int expected)
    {
        int port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            ctx.Response.StatusCode = (int)status;
            ctx.Response.Close();
        });

        int code = await HealthCheckCommand.RunAsync(new Uri($"http://127.0.0.1:{port}/health"), TimeSpan.FromSeconds(5));
        await serve;

        Assert.Equal(expected, code);
    }

    [Fact]
    public async Task Exit_code_is_1_when_nothing_is_listening()
    {
        int code = await HealthCheckCommand.RunAsync(new Uri($"http://127.0.0.1:{FreePort()}/health"), TimeSpan.FromSeconds(5));
        Assert.Equal(1, code);
    }

    private static int FreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        return ((IPEndPoint)l.LocalEndpoint).Port;
    }
}
