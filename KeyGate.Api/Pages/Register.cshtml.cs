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

    public string? ErrorMessage { get; private set; }
    public string? FullName { get; set; }
    public string? EmailOrEmployeeId { get; set; }
    public string? Department { get; set; }
    public string? Sex { get; set; }
    public int? Age { get; set; }
    public string? Province { get; set; }
    public string? CityMunicipality { get; set; }
    public string? Barangay { get; set; }
    public string? Sectors { get; set; }
    public string? ServiceAvailed { get; set; }
    public string? AccessKey { get; private set; }
    public bool ShowForm { get; private set; } = true;

    public void OnGet()
    {
        ShowForm = true;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        FullName = Request.Form["FullName"].ToString().Trim();
        EmailOrEmployeeId = Request.Form["EmailOrEmployeeId"].ToString().Trim();
        Department = Request.Form["Department"].ToString().Trim();
        Sex = Request.Form["Sex"].ToString().Trim();
        Province = Request.Form["Province"].ToString().Trim();
        CityMunicipality = Request.Form["CityMunicipality"].ToString().Trim();
        Barangay = Request.Form["Barangay"].ToString().Trim();
        ServiceAvailed = Request.Form["ServiceAvailed"].ToString().Trim();

        if (int.TryParse(Request.Form["Age"].ToString().Trim(), out var ageVal))
        {
            Age = ageVal;
        }

        var sectorValues = Request.Form["SectorValues"].ToList();
        Sectors = sectorValues.Count > 0
            ? JsonSerializer.Serialize(sectorValues)
            : null;

        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(EmailOrEmployeeId))
        {
            ErrorMessage = "Full name and Email / Employee ID are required.";
            ShowForm = true;
            return Page();
        }

        using var client = CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/registration/self-register", new
        {
            FullName,
            EmailOrEmployeeId,
            Department,
            Sex,
            Age,
            Province,
            CityMunicipality,
            Barangay,
            Sectors,
            ServiceAvailed
        });

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ReadErrorAsync(response);
            ShowForm = true;
            return Page();
        }

        var result = await response.Content.ReadFromJsonAsync<CompleteResult>();
        if (result is null)
        {
            ErrorMessage = "Could not complete your registration. Please try again.";
            ShowForm = true;
            return Page();
        }

        AccessKey = result.AccessKey;
        ShowForm = false;
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
        }

        return $"Registration failed ({(int)response.StatusCode}).";
    }

    private sealed record CompleteResult(string AccessKey, string FullName, string EmailOrEmployeeId);
}
