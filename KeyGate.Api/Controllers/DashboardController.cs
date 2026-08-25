using KeyGate.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyGate.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Admin")]
public class DashboardController : ControllerBase
{
    private readonly KeyGateDbContext _db;

    public DashboardController(KeyGateDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var individualsCount = await _db.Individuals.CountAsync();

        var devices = await _db.Devices
            .Select(d => new
            {
                d.Id,
                d.DeviceName,
                d.Status,
                d.Location,
                d.LastSeenAt,
                CurrentIndividualName = d.Sessions
                    .Where(s => s.EndedAt == null)
                    .OrderByDescending(s => s.StartedAt)
                    .Select(s => (string?)s.Individual.FullName)
                    .FirstOrDefault()
            })
            .OrderBy(d => d.DeviceName)
            .ToListAsync();

        var sessionsQuery = _db.Sessions
            .Include(s => s.Individual)
            .Include(s => s.Device)
            .AsQueryable();

        if (from is not null)
            sessionsQuery = sessionsQuery.Where(s => s.StartedAt >= from);
        if (to is not null)
            sessionsQuery = sessionsQuery.Where(s => s.StartedAt <= to);

        var filteredSessions = await sessionsQuery
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        var chartSessions = filteredSessions.Select(s => new
        {
            DeviceName = s.Device?.DeviceName ?? string.Empty,
            s.StartedAt,
            s.EndedAt,
            s.DurationSeconds,
            EndReason = s.EndReason?.ToString()
        }).ToList();

        return Ok(new
        {
            individualsCount,
            devices,
            sessions = chartSessions
        });
    }
}
