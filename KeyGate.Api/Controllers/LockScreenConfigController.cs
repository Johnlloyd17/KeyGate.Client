using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using KeyGate.Api.Hubs;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    private readonly IHubContext<DeviceStatusHub> _hub;

    public LockScreenConfigController(KeyGateDbContext db, DeviceAuthService deviceAuth, IWebHostEnvironment environment, IHubContext<DeviceStatusHub> hub)
    {
        _db = db;
        _deviceAuth = deviceAuth;
        _environment = environment;
        _hub = hub;
    }

    public record SaveConfigRequest(int? DeviceId, string? BackgroundImageUrl, string? LogoUrl, string? Title, string? Subtitle, string? ScheduledLogoutTime);

    public record ConfigResponse(
        int? DeviceId,
        string? BackgroundImageUrl,
        string? LogoUrl,
        string? Title,
        string? Subtitle,
        string? ScheduledLogoutTime,
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
            return Ok(new ConfigResponse(null, null, null, null, null, null, DateTime.MinValue, "default"));
        }

        var source = config.DeviceId is null ? "global" : "device";

        return Ok(new ConfigResponse(
            config.DeviceId,
            config.BackgroundImageUrl,
            config.LogoUrl,
            config.Title,
            config.Subtitle,
            config.ScheduledLogoutTime,
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

        var changedBy = User.Identity?.Name ?? "Admin";
        var logs = new List<ConfigChangeLog>();

        if (config.Title != request.Title)
        {
            logs.Add(new ConfigChangeLog { DeviceId = request.DeviceId, ChangedBy = changedBy, FieldChanged = "Title", OldValue = config.Title, NewValue = request.Title, ChangedAt = DateTime.UtcNow });
        }
        if (config.Subtitle != request.Subtitle)
        {
            logs.Add(new ConfigChangeLog { DeviceId = request.DeviceId, ChangedBy = changedBy, FieldChanged = "Subtitle", OldValue = config.Subtitle, NewValue = request.Subtitle, ChangedAt = DateTime.UtcNow });
        }
        if (config.BackgroundImageUrl != request.BackgroundImageUrl)
        {
            logs.Add(new ConfigChangeLog { DeviceId = request.DeviceId, ChangedBy = changedBy, FieldChanged = "BackgroundImage", OldValue = config.BackgroundImageUrl, NewValue = request.BackgroundImageUrl, ChangedAt = DateTime.UtcNow });
        }
        if (config.LogoUrl != request.LogoUrl)
        {
            logs.Add(new ConfigChangeLog { DeviceId = request.DeviceId, ChangedBy = changedBy, FieldChanged = "Logo", OldValue = config.LogoUrl, NewValue = request.LogoUrl, ChangedAt = DateTime.UtcNow });
        }
        if (config.ScheduledLogoutTime != request.ScheduledLogoutTime)
        {
            logs.Add(new ConfigChangeLog { DeviceId = request.DeviceId, ChangedBy = changedBy, FieldChanged = "ScheduledLogoutTime", OldValue = config.ScheduledLogoutTime, NewValue = request.ScheduledLogoutTime, ChangedAt = DateTime.UtcNow });
        }

        config.BackgroundImageUrl = request.BackgroundImageUrl ?? config.BackgroundImageUrl;
        config.LogoUrl = request.LogoUrl ?? config.LogoUrl;
        config.Title = request.Title ?? config.Title;
        config.Subtitle = request.Subtitle ?? config.Subtitle;
        config.ScheduledLogoutTime = string.IsNullOrWhiteSpace(request.ScheduledLogoutTime) ? null : request.ScheduledLogoutTime;
        config.UpdatedAt = DateTime.UtcNow;

        if (logs.Count > 0)
        {
            _db.ConfigChangeLogs.AddRange(logs);
        }

        var versionSetting = await _db.SystemSettings.SingleOrDefaultAsync(s => s.Key == "ConfigVersion");
        if (versionSetting is not null)
        {
            versionSetting.Value = (int.Parse(versionSetting.Value) + 1).ToString();
            versionSetting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        await BroadcastConfigChangedAsync(request.DeviceId);

        return Ok(new ConfigResponse(
            config.DeviceId,
            config.BackgroundImageUrl,
            config.LogoUrl,
            config.Title,
            config.Subtitle,
            config.ScheduledLogoutTime,
            config.UpdatedAt,
            config.DeviceId is null ? "global" : "device"));
    }

    [HttpGet("version")]
    public async Task<IActionResult> GetConfigVersion([FromQuery] int? deviceId)
    {
        var setting = await _db.SystemSettings.SingleOrDefaultAsync(s => s.Key == "ConfigVersion");
        var version = setting is not null ? int.Parse(setting.Value) : 1;
        return Ok(new { version });
    }

    [HttpGet("history")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetHistory([FromQuery] int? deviceId)
    {
        var query = _db.ConfigChangeLogs.AsQueryable();
        if (deviceId is not null)
            query = query.Where(l => l.DeviceId == deviceId);
        else
            query = query.Where(l => l.DeviceId == null);

        var logs = await query
            .OrderByDescending(l => l.ChangedAt)
            .Take(50)
            .Select(l => new { l.Id, l.DeviceId, l.ChangedBy, l.FieldChanged, l.OldValue, l.NewValue, l.ChangedAt })
            .ToListAsync();

        return Ok(logs);
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

    private async Task BroadcastConfigChangedAsync(int? deviceId)
    {
        var @event = new DeviceStatusHub.LockScreenConfigChangedEvent(
            deviceId,
            DateTime.UtcNow);

        await _hub.Clients.All.SendAsync(DeviceStatusHub.LockScreenConfigChangedMethod, @event);
    }
}
