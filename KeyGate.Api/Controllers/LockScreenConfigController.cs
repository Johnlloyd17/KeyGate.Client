using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyGate.Api.Controllers;

[ApiController]
[Route("api/lockscreen-config")]
public class LockScreenConfigController : ControllerBase
{
    private static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };

    private readonly KeyGateDbContext _db;
    private readonly DeviceAuthService _deviceAuth;
    private readonly IWebHostEnvironment _environment;

    public LockScreenConfigController(KeyGateDbContext db, DeviceAuthService deviceAuth, IWebHostEnvironment environment)
    {
        _db = db;
        _deviceAuth = deviceAuth;
        _environment = environment;
    }

    public record SaveConfigRequest(int? DeviceId, string? BackgroundImageUrl, string? LogoUrl, string? Title);

    public record ConfigResponse(
        int? DeviceId,
        string? BackgroundImageUrl,
        string? LogoUrl,
        string? Title,
        DateTime UpdatedAt,
        string Source);

    [HttpGet]
    public async Task<IActionResult> GetConfig([FromQuery] int? deviceId)
    {
        Device? device = null;
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin)
        {
            var deviceIdHeader = Request.Headers.TryGetValue("X-Device-Id", out var idHeader) && int.TryParse(idHeader, out var parsed)
                ? parsed
                : deviceId;

            var apiKey = Request.Headers.TryGetValue("X-Device-Api-Key", out var keyHeader) ? keyHeader.ToString() : null;

            device = await _deviceAuth.ValidateAsync(deviceIdHeader, apiKey);
            if (device is null)
            {
                return Unauthorized(new { message = "Valid device credentials or admin authentication are required." });
            }
        }

        var effectiveDeviceId = device?.Id ?? deviceId;

        var config = effectiveDeviceId is not null
            ? await _db.LockScreenConfigs
                .Where(c => c.DeviceId == effectiveDeviceId)
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync()
            : await _db.LockScreenConfigs
                .Where(c => c.DeviceId == null)
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync();

        if (config is null && effectiveDeviceId is not null)
        {
            config = await _db.LockScreenConfigs
                .Where(c => c.DeviceId == null)
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        if (config is null)
        {
            return Ok(new ConfigResponse(null, null, null, null, DateTime.MinValue, "default"));
        }

        var source = config.DeviceId is null ? "global" : "device";

        return Ok(new ConfigResponse(
            config.DeviceId,
            config.BackgroundImageUrl,
            config.LogoUrl,
            config.Title,
            config.UpdatedAt,
            source));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveConfig([FromBody] SaveConfigRequest request)
    {
        var config = await _db.LockScreenConfigs
            .Where(c => c.DeviceId == request.DeviceId)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync();

        if (config is null)
        {
            config = new LockScreenConfig
            {
                DeviceId = request.DeviceId,
                UpdatedAt = DateTime.UtcNow
            };
            _db.LockScreenConfigs.Add(config);
        }

        config.BackgroundImageUrl = request.BackgroundImageUrl ?? config.BackgroundImageUrl;
        config.LogoUrl = request.LogoUrl ?? config.LogoUrl;
        config.Title = request.Title ?? config.Title;
        config.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ConfigResponse(
            config.DeviceId,
            config.BackgroundImageUrl,
            config.LogoUrl,
            config.Title,
            config.UpdatedAt,
            config.DeviceId is null ? "global" : "device"));
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedImageExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Unsupported file type. Allowed: PNG, JPG, JPEG, GIF, WEBP, BMP." });
        }

        var uploadsDir = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

        return Ok(new { url });
    }
}
