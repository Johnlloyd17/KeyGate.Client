using System.Text;
using System.Threading.RateLimiting;
using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using KeyGate.Api.Hubs;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<KeyGateDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("KeyGateDb")));

builder.Services.AddScoped<KeyHashingService>();
builder.Services.AddScoped<QrCodeService>();
builder.Services.AddScoped<DeviceAuthService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddSignalR();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("unlock", context =>
    {
        var partitionKey = context.Request.Headers["X-Device-Id"].ToString();
        if (string.IsNullOrWhiteSpace(partitionKey))
        {
            partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var jwtSettings = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = signingKey
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KeyGateDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var hashing = scope.ServiceProvider.GetRequiredService<KeyHashingService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var seedSection = configuration.GetSection("AdminSeed");
    if (seedSection.Exists())
    {
        try
        {
            if (!db.Admins.Any(a => a.Email == seedSection["Email"]))
            {
                db.Admins.Add(new Admin
                {
                    FullName = seedSection["FullName"] ?? "KeyGate Administrator",
                    Email = seedSection["Email"] ?? "admin@keygate.local",
                    PasswordHash = hashing.Hash(seedSection["Password"] ?? "ChangeMe123!"),
                    Role = seedSection["Role"] ?? "Admin",
                    CreatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
                logger.LogInformation("Seeded default admin '{Email}'.", seedSection["Email"]);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not seed default admin. Is the database available? Run the EF migrations first (section 6.5).");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapHub<DeviceStatusHub>("/hubs/devices");

app.Run();
