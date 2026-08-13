using System.Security.Claims;
using System.Text.Json;
using KeyGate.Admin.Components;
using KeyGate.Admin.Models;
using KeyGate.Admin.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = "KeyGate.Admin";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpClient("KeyGateApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["KeyGateApi:BaseUrl"] ?? "http://localhost:5000/");
});

builder.Services.AddScoped<AdminApiClient>();
builder.Services.AddScoped<DeviceStatusListener>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapPost("/signin", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    var form = await context.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var client = httpClientFactory.CreateClient("KeyGateApi");
    var response = await client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest(email, password));

    if (!response.IsSuccessStatusCode)
    {
        return Results.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
    }

    var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
    if (login is null)
    {
        return Results.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, login.FullName),
        new(ClaimTypes.Email, login.Email),
        new(ClaimTypes.Role, login.Role),
        new("jwt", login.Token)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties { IsPersistent = true });

    var safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/") ? "/" : returnUrl;
    return Results.Redirect(safeReturnUrl);
});

app.MapPost("/signout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
