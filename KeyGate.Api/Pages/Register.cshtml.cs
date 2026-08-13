using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KeyGate.Api.Pages;

public class RegisterModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RegisterModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private sealed record RegistrationInfo(
        string FullName,
        string EmailOrEmployeeId,
        string? Department,
        DateTime ExpiresAt);

    private sealed record CompleteResult(string AccessKey, string FullName, string EmailOrEmployeeId);

    public string? ErrorMessage { get; private set; }
    public string? FullName { get; private set; }
    public string? EmailOrEmployeeId { get; private set; }
    public string? Department { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public string? AccessKey { get; private set; }

    public async Task<IActionResult> OnGetAsync(string token)
    {
        if (!Guid.TryParse(token, out _))
        {
            ErrorMessage = "Invalid registration link.";
            return Page();
        }

        using var client = CreateApiClient();
        var response = await client.GetAsync($"/api/registration/{token}");

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ReadErrorAsync(response);
            return Page();
        }

        var info = await response.Content.ReadFromJsonAsync<RegistrationInfo>();
        if (info is null)
        {
            ErrorMessage = "Could not load your registration details.";
            return Page();
        }

        FullName = info.FullName;
        EmailOrEmployeeId = info.EmailOrEmployeeId;
        Department = info.Department;
        ExpiresAt = info.ExpiresAt;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string token)
    {
        if (!Guid.TryParse(token, out _))
        {
            ErrorMessage = "Invalid registration link.";
            return Page();
        }

        using var client = CreateApiClient();
        var response = await client.PostAsync($"/api/registration/{token}/complete", content: null);

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ReadErrorAsync(response);
            return Page();
        }

        var result = await response.Content.ReadFromJsonAsync<CompleteResult>();
        if (result is null)
        {
            ErrorMessage = "Could not complete your registration. Please try again.";
            return Page();
        }

        FullName = result.FullName;
        EmailOrEmployeeId = result.EmailOrEmployeeId;
        AccessKey = result.AccessKey;

        return Page();
    }

    private HttpClient CreateApiClient()
    {
        var client = _httpClientFactory.CreateClient("RegistrationPage");
        client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");
        return client;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? $"Registration failed ({(int)response.StatusCode}).";
            }
        }
        catch (JsonException)
        {
            // fall through to generic message
        }

        return $"Registration failed ({(int)response.StatusCode}).";
    }
}
