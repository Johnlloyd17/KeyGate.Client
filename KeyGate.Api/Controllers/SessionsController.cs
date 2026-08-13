using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyGate.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly KeyGateDbContext _db;
    private readonly SessionService _sessionService;
    private readonly DeviceAuthService _deviceAuth;

    public SessionsController(KeyGateDbContext db, SessionService sessionService, DeviceAuthService deviceAuth)
    {
        _db = db;
        _sessionService = sessionService;
        _deviceAuth = deviceAuth;
    }

    public record UnlockRequest(string Key, int DeviceId);

    public record EndSessionRequest(string? EndReason);

    public record SessionDto(
        int Id,
        int IndividualId,
        string IndividualName,
        int DeviceId,
        string DeviceName,
        DateTime StartedAt,
        DateTime? EndedAt,
        int? DurationSeconds,
        string? EndReason);

    [HttpPost("unlock")]
    [AllowAnonymous]
    public async Task<IActionResult> Unlock([FromBody] UnlockRequest request)
    {
        var device = await GetAuthenticatedDeviceAsync();
        if (device is null)
        {
            return Unauthorized(new { message = "Valid device credentials are required." });
        }

        if (device.Id != request.DeviceId)
        {
            return BadRequest(new { message = "DeviceId does not match the authenticated device." });
        }

        var result = await _sessionService.UnlockAsync(device.Id, request.Key);

        if (result.Succeeded)
        {
            return Ok(new { sessionId = result.SessionId, individualName = result.IndividualName, startedAt = DateTime.UtcNow });
        }

        return result.FailureReason switch
        {
            UnlockFailureReason.DeviceNotFound => NotFound(new { message = result.Message }),
            UnlockFailureReason.DeviceUnlocked => Conflict(new { message = result.Message }),
            UnlockFailureReason.KeyActiveElsewhere => Conflict(new { message = result.Message }),
            UnlockFailureReason.ConcurrentConflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpPost("{id:int}/end")]
    [AllowAnonymous]
    public async Task<IActionResult> EndSession(int id, [FromBody] EndSessionRequest? request)
    {
        var device = await GetAuthenticatedDeviceAsync();
        if (device is null)
        {
            return Unauthorized(new { message = "Valid device credentials are required." });
        }

        var session = await _db.Sessions
            .Include(s => s.Device)
            .SingleOrDefaultAsync(s => s.Id == id);

        if (session is null)
        {
            return NotFound(new { message = "Session not found." });
        }

        if (session.DeviceId != device.Id)
        {
            return BadRequest(new { message = "Session does not belong to this device." });
        }

        if (session.EndReason is null && !string.IsNullOrWhiteSpace(request?.EndReason)
            && Enum.TryParse<SessionEndReason>(request.EndReason, ignoreCase: true, out var reason))
        {
            session.EndReason = reason;
            await _db.SaveChangesAsync();
        }

        var ended = await _sessionService.EndSessionAsync(id);
        if (ended is null)
        {
            return NotFound(new { message = "Session not found." });
        }

        return Ok(new SessionDto(
            ended.Id,
            ended.IndividualId,
            ended.Individual?.FullName ?? string.Empty,
            ended.DeviceId,
            ended.Device?.DeviceName ?? string.Empty,
            ended.StartedAt,
            ended.EndedAt,
            ended.DurationSeconds,
            ended.EndReason?.ToString()));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] int? individualId,
        [FromQuery] int? deviceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = _db.Sessions
            .Include(s => s.Individual)
            .Include(s => s.Device)
            .AsQueryable();

        if (individualId is not null)
        {
            query = query.Where(s => s.IndividualId == individualId);
        }
        if (deviceId is not null)
        {
            query = query.Where(s => s.DeviceId == deviceId);
        }
        if (from is not null)
        {
            query = query.Where(s => s.StartedAt >= from);
        }
        if (to is not null)
        {
            query = query.Where(s => s.StartedAt <= to);
        }

        var sessions = await query
            .OrderByDescending(s => s.StartedAt)
            .Take(500)
            .ToListAsync();

        var result = sessions.Select(s => new SessionDto(
            s.Id,
            s.IndividualId,
            s.Individual?.FullName ?? string.Empty,
            s.DeviceId,
            s.Device?.DeviceName ?? string.Empty,
            s.StartedAt,
            s.EndedAt,
            s.DurationSeconds,
            s.EndReason?.ToString()))
            .ToList();

        return Ok(result);
    }

    private async Task<Device?> GetAuthenticatedDeviceAsync()
    {
        if (!Request.Headers.TryGetValue("X-Device-Id", out var idHeader) || !int.TryParse(idHeader, out var deviceId))
        {
            return null;
        }

        var apiKey = Request.Headers.TryGetValue("X-Device-Api-Key", out var keyHeader) ? keyHeader.ToString() : null;

        return await _deviceAuth.ValidateAsync(deviceId, apiKey);
    }
}
