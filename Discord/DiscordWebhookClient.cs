using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace StatusImageCard.Discord;

public sealed class DiscordWebhookClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;

    public DiscordWebhookClient(HttpClient http, JsonSerializerOptions json)
    {
        _http = http;
        _json = json;
    }

    // URL-based (non-attachment) send/edit, kept for convenience
    public async Task<string> SendNewAsync(string webhookUrl, object payload, CancellationToken ct)
    {
        var res = await _http.PostAsJsonAsync($"{webhookUrl.TrimEnd('/')}?wait=true", payload, _json, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var id = doc.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Discord response missing message id.");

        return id!;
    }

    public async Task<HttpStatusCode> EditAsync(string webhookUrl, string messageId, object payload, CancellationToken ct)
    {
        var editUrl = $"{webhookUrl.TrimEnd('/')}/messages/{messageId}";
        using var req = new HttpRequestMessage(HttpMethod.Patch, editUrl)
        {
            Content = JsonContent.Create(payload, options: _json)
        };

        using var res = await _http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.NotFound)
            return res.StatusCode;

        if ((int)res.StatusCode >= 400)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Discord edit failed {(int)res.StatusCode} {res.StatusCode}: {body}");
        }

        return res.StatusCode;
    }

    // Attachment-based send/edit (payload_json + files[0]) - matches Discord docs
    public async Task<string> SendNewWithFileAsync(
        string webhookUrl,
        object payload,
        byte[] fileBytes,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();

        var payloadJson = JsonSerializer.Serialize(payload, _json);
        form.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json");

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "files[0]", fileName);

        using var res = await _http.PostAsync($"{webhookUrl.TrimEnd('/')}?wait=true", form, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var id = doc.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Discord response missing message id.");

        return id!;
    }

    public async Task<HttpStatusCode> EditWithFileAsync(
        string webhookUrl,
        string messageId,
        object payload,
        byte[] fileBytes,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        var editUrl = $"{webhookUrl.TrimEnd('/')}/messages/{messageId}";

        using var form = new MultipartFormDataContent();

        var payloadJson = JsonSerializer.Serialize(payload, _json);
        form.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json");

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "files[0]", fileName);

        using var req = new HttpRequestMessage(HttpMethod.Patch, editUrl) { Content = form };
        using var res = await _http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.NotFound)
            return res.StatusCode;

        if ((int)res.StatusCode >= 400)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Discord edit failed {(int)res.StatusCode} {res.StatusCode}: {body}");
        }

        return res.StatusCode;
    }
}
