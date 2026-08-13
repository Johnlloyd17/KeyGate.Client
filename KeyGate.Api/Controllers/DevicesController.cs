using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyGate.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly KeyGateDbContext _db;
    private readonly DeviceAuthService _deviceAuth;

    public DevicesController(KeyGateDbContext db, DeviceAuthService deviceAuth)
    {
        _db = db;
        _deviceAuth = deviceAuth;
    }

    public record RegisterDeviceRequest(string DeviceName, string DeviceFingerprint, string? Location);

    public record RegisterDeviceResponse(int DeviceId, string DeviceApiKey, string DeviceName, string Status);

    public record UpdateDeviceRequest(string? DeviceName, string? Location);

    public record DeviceDto(
        int Id,
        string DeviceName,
        string DeviceFingerprint,
        string? Location,
        string Status,
        DateTime LastSeenAt,
        int? CurrentSessionId,
        string? CurrentIndividualName);

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName) || string.IsNullOrWhiteSpace(request.DeviceFingerprint))
        {
            return BadRequest(new { message = "DeviceName and DeviceFingerprint are required." });
        }

        var device = await _db.Devices.SingleOrDefaultAsync(d => d.DeviceFingerprint == request.DeviceFingerprint);

        var apiKey = DeviceAuthService.IssueApiKey();

        if (device is null)
        {
            device = new Device
            {
                DeviceName = request.DeviceName,
                DeviceFingerprint = request.DeviceFingerprint,
                DeviceApiKeyHash = _deviceAuth.HashApiKey(apiKey),
                Location = request.Location,
                Status = DeviceStatus.Locked,
                LastSeenAt = DateTime.UtcNow
            };
            _db.Devices.Add(device);
        }
        else
        {
            device.DeviceName = request.DeviceName;
            device.DeviceApiKeyHash = _deviceAuth.HashApiKey(apiKey);
            device.Location = request.Location ?? device.Location;
            device.LastSeenAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new RegisterDeviceResponse(device.Id, apiKey, device.DeviceName, device.Status.ToString()));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDevices()
    {
        var devices = await _db.Devices
            .Select(d => new
            {
                d.Id,
                d.DeviceName,
                d.DeviceFingerprint,
                d.Location,
                d.Status,
                d.LastSeenAt,
                CurrentSessionId = (int?)d.Sessions
                    .Where(s => s.EndedAt == null)
                    .OrderByDescending(s => s.StartedAt)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefault(),
                CurrentIndividualName = d.Sessions
                    .Where(s => s.EndedAt == null)
                    .OrderByDescending(s => s.StartedAt)
                    .Select(s => (string?)s.Individual.FullName)
                    .FirstOrDefault()
            })
            .OrderBy(d => d.DeviceName)
            .ToListAsync();

        var result = devices.Select(d => new DeviceDto(
            d.Id,
            d.DeviceName,
            d.DeviceFingerprint,
            d.Location,
            d.Status.ToString(),
            d.LastSeenAt,
            d.CurrentSessionId,
            d.CurrentIndividualName))
            .ToList();

        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDevice(int id, [FromBody] UpdateDeviceRequest request)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null)
        {
            return NotFound(new { message = "Device not found." });
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceName))
        {
            device.DeviceName = request.DeviceName;
        }
        if (request.Location is not null)
        {
            device.Location = request.Location;
        }

        await _db.SaveChangesAsync();

        var currentSession = await _db.Sessions
            .Where(s => s.DeviceId == device.Id && s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new { s.Id, s.Individual.FullName })
            .FirstOrDefaultAsync();

        return Ok(new DeviceDto(
            device.Id,
            device.DeviceName,
            device.DeviceFingerprint,
            device.Location,
            device.Status.ToString(),
            device.LastSeenAt,
            currentSession?.Id,
            currentSession?.FullName));
    }
}
