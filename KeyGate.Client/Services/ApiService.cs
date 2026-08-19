using System.Text;
using System.Text.Json;
using KeyGate.Client.Models;

namespace KeyGate.Client.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly DeviceIdentityService _identity;
    private readonly AppSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiService(DeviceIdentityService identity, AppSettings settings)
    {
        _identity = identity;
        _settings = settings;
        _http = new HttpClient
        {
            BaseAddress = new Uri(_settings.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task EnsureDeviceRegisteredAsync()
    {
        if (_identity.IsRegistered)
        {
            return;
        }

        var payload = new
        {
            deviceName = _identity.GetDeviceName(),
            deviceFingerprint = _identity.GetDeviceFingerprint(),
            location = (string?)null
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/devices/register", content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var deviceId = root.GetProperty("deviceId").GetInt32();
        var apiKey = root.GetProperty("deviceApiKey").GetString() ?? string.Empty;

        _identity.SetDeviceCredentials(deviceId, apiKey);
    }

    public async Task<LockScreenConfig?> GetLockScreenConfigAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/lockscreen-config");
        AddDeviceHeaders(request);

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<LockScreenConfig>(body, JsonOptions);
    }

    public async Task<int> GetConfigVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/lockscreen-config/version");
            AddDeviceHeaders(request);

            var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("version").GetInt32();
        }
        catch
        {
            return 0;
        }
    }

    public async Task<(SessionInfo? Session, string? Error)> UnlockAsync(string key, CancellationToken cancellationToken = default)
    {
        var deviceId = _identity.GetDeviceId()
            ?? throw new InvalidOperationException("Device is not registered with the KeyGate server.");

        var request = new UnlockRequest { Key = key, DeviceId = deviceId };
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/sessions/unlock");
        AddDeviceHeaders(message);
        message.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (null, TryGetErrorMessage(body) ?? $"Server returned {response.StatusCode}.");
        }

        var session = JsonSerializer.Deserialize<SessionInfo>(body, JsonOptions);
        return (session, null);
    }

    public async Task<bool> EndSessionAsync(int sessionId, string endReason = "Logout")
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{sessionId}/end");
            AddDeviceHeaders(message);
            message.Content = new StringContent(
                JsonSerializer.Serialize(new { endReason }),
                Encoding.UTF8,
                "application/json");

            var response = await _http.SendAsync(message);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void AddDeviceHeaders(HttpRequestMessage request)
    {
        if (_identity.GetDeviceId() is int deviceId)
        {
            request.Headers.TryAddWithoutValidation("X-Device-Id", deviceId.ToString());
        }
        if (!string.IsNullOrWhiteSpace(_identity.GetDeviceApiKey()))
        {
            request.Headers.TryAddWithoutValidation("X-Device-Api-Key", _identity.GetDeviceApiKey());
        }
    }

    private static string? TryGetErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
