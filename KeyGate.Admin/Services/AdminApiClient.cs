using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using KeyGate.Admin.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace KeyGate.Admin.Services;

public class KeyGateApiException : Exception
{
    public KeyGateApiException(string message) : base(message) { }
}

public class AdminApiClient
{
    public const string JwtClaimType = "jwt";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthenticationStateProvider _authStateProvider;

    public AdminApiClient(IHttpClientFactory httpClientFactory, AuthenticationStateProvider authStateProvider)
    {
        _httpClientFactory = httpClientFactory;
        _authStateProvider = authStateProvider;
    }

    public async Task<string?> GetJwtAsync()
    {
        var state = await _authStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirstValue(JwtClaimType);
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        var client = _httpClientFactory.CreateClient("KeyGateApi");
        var jwt = await GetJwtAsync();
        if (!string.IsNullOrEmpty(jwt))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        }
        return client;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var bodyText = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new KeyGateApiException("Your session has expired. Please sign in again.");
        }

        string message = $"Request failed. ({(int)response.StatusCode} {response.StatusCode}) {bodyText}";

        throw new KeyGateApiException(message);
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        var client = _httpClientFactory.CreateClient("KeyGateApi");
        var response = await client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest(email, password));
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    public async Task<List<IndividualDto>> GetIndividualsAsync()
    {
        var client = await CreateClientAsync();
        var response = await client.GetAsync("/api/individuals");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<IndividualDto>>() ?? new();
    }

    public async Task<IndividualDto> CreateIndividualAsync(string fullName, string emailOrEmployeeId, string? department)
    {
        var client = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/individuals", new CreateIndividualRequest(fullName, emailOrEmployeeId, department));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<IndividualDto>() ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task<IndividualDto> UpdateIndividualAsync(
        int id,
        string fullName,
        string emailOrEmployeeId,
        string? department,
        string? sex,
        int? age,
        string? province,
        string? cityMunicipality,
        string? barangay,
        string? sectors,
        string? serviceAvailed)
    {
        var client = await CreateClientAsync();
        var response = await client.PutAsJsonAsync($"/api/individuals/{id}", new UpdateIndividualRequest(fullName, emailOrEmployeeId, department, sex, age, province, cityMunicipality, barangay, sectors, serviceAvailed));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<IndividualDto>() ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task DeleteIndividualAsync(int id)
    {
        var client = await CreateClientAsync();
        var response = await client.DeleteAsync($"/api/individuals/{id}");
        await EnsureSuccessAsync(response);
    }

    public async Task<RegenerateTokenResponse> RegenerateTokenAsync(int id)
    {
        var client = await CreateClientAsync();
        var response = await client.PostAsync($"/api/individuals/{id}/regenerate-token", null);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<RegenerateTokenResponse>() ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task<List<DeviceDto>> GetDevicesAsync()
    {
        var client = await CreateClientAsync();
        var response = await client.GetAsync("/api/devices");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<DeviceDto>>() ?? new();
    }

    public async Task<DeviceDto> UpdateDeviceAsync(int id, string? deviceName, string? location)
    {
        var client = await CreateClientAsync();
        var response = await client.PutAsJsonAsync($"/api/devices/{id}", new UpdateDeviceRequest(deviceName, location));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<DeviceDto>() ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task<LockScreenConfigDto> GetLockScreenConfigAsync(int? deviceId)
    {
        var client = await CreateClientAsync();
        var url = deviceId is null ? "/api/lockscreen-config" : $"/api/lockscreen-config?deviceId={deviceId}";
        var response = await client.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LockScreenConfigDto>() ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task<LockScreenConfigDto> SaveLockScreenConfigAsync(int? deviceId, string? backgroundImageUrl, string? logoUrl, string? title, string? subtitle, string? scheduledLogoutTime)
    {
        var client = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/lockscreen-config", new SaveLockScreenConfigRequest(deviceId, backgroundImageUrl, logoUrl, title, subtitle, scheduledLogoutTime));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LockScreenConfigDto>() ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task<List<ConfigChangeLogDto>> GetConfigHistoryAsync(int? deviceId)
    {
        var client = await CreateClientAsync();
        var url = deviceId is null ? "/api/lockscreen-config/history" : $"/api/lockscreen-config/history?deviceId={deviceId}";
        var response = await client.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ConfigChangeLogDto>>() ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName)
    {
        var client = await CreateClientAsync();
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);

        var response = await client.PostAsync("/api/lockscreen-config/upload", content);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
        return result?.Url ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task<List<SessionDto>> GetSessionsAsync(int? individualId = null, int? deviceId = null, DateTime? from = null, DateTime? to = null)
    {
        var client = await CreateClientAsync();
        var query = new List<string>();
        if (individualId is not null) query.Add($"individualId={individualId}");
        if (deviceId is not null) query.Add($"deviceId={deviceId}");
        if (from is not null) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        if (to is not null) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        var url = query.Count > 0 ? $"/api/sessions?{string.Join("&", query)}" : "/api/sessions";

        var response = await client.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<SessionDto>>() ?? new();
    }

    public async Task<string> GenerateQrPngBase64Async(string payload)
    {
        var client = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/registration/qr", new { url = payload });
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("qrCodePngBase64").GetString() ?? throw new KeyGateApiException("Could not generate QR code.");
    }

    public async Task<byte[]> ExportIndividualsAsync(string format, HashSet<string>? columns = null)
    {
        var client = await CreateClientAsync();
        var url = $"/api/import-export/individuals?format={format}";
        if (columns is not null && columns.Count > 0)
            url += $"&columns={string.Join(",", columns)}";
        var response = await client.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<byte[]> ExportDevicesAsync(string format, HashSet<string>? columns = null)
    {
        var client = await CreateClientAsync();
        var url = $"/api/import-export/devices?format={format}";
        if (columns is not null && columns.Count > 0)
            url += $"&columns={string.Join(",", columns)}";
        var response = await client.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<byte[]> ExportSessionsAsync(string format, HashSet<string>? columns = null)
    {
        var client = await CreateClientAsync();
        var url = $"/api/import-export/sessions?format={format}";
        if (columns is not null && columns.Count > 0)
            url += $"&columns={string.Join(",", columns)}";
        var response = await client.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<ImportResult> ImportIndividualsAsync(Stream fileStream, string fileName)
    {
        var client = await CreateClientAsync();
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        content.Add(fileContent, "file", fileName);
        var response = await client.PostAsync("/api/import-export/individuals", content);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ImportResult>() ?? new ImportResult(0, 0, 0, new());
    }

    public async Task<ImportResult> ImportDevicesAsync(Stream fileStream, string fileName)
    {
        var client = await CreateClientAsync();
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        content.Add(fileContent, "file", fileName);
        var response = await client.PostAsync("/api/import-export/devices", content);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ImportResult>() ?? new ImportResult(0, 0, 0, new());
    }

    public async Task<AdminAccountDto> GetMyAccountAsync()
    {
        var client = await CreateClientAsync();
        var response = await client.GetAsync("/api/admin/account");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AdminAccountDto>() ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task<AdminAccountDto> UpdateMyProfileAsync(string fullName, string email, string? phone, string? position)
    {
        var client = await CreateClientAsync();
        var response = await client.PutAsJsonAsync("/api/admin/account", new UpdateAdminProfileRequest(fullName, email, phone, position));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AdminAccountDto>() ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task<string> UploadAvatarAsync(Stream fileStream, string fileName)
    {
        var client = await CreateClientAsync();
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        content.Add(fileContent, "file", fileName);
        var response = await client.PostAsync("/api/admin/account/avatar", content);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
        return result?.Url ?? throw new KeyGateApiException("Could not read the API response.");
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var client = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/admin/account/password", new ChangePasswordRequest(currentPassword, newPassword));
        await EnsureSuccessAsync(response);
    }
}
