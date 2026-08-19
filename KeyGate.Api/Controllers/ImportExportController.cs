using System.Security.Claims;
using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyGate.Api.Controllers;

[ApiController]
[Route("api/import-export")]
[Authorize(Roles = "Admin")]
public class ImportExportController : ControllerBase
{
    private readonly KeyGateDbContext _db;
    private readonly SpreadsheetService _spreadsheet;

    public ImportExportController(KeyGateDbContext db, SpreadsheetService spreadsheet)
    {
        _db = db;
        _spreadsheet = spreadsheet;
    }

    private static HashSet<string>? ParseColumns(string? columns)
    {
        if (string.IsNullOrWhiteSpace(columns))
            return null;
        var set = columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return set.Count > 0 ? set : null;
    }

    [HttpGet("individuals")]
    public async Task<IActionResult> ExportIndividuals([FromQuery] string format = "xlsx", [FromQuery] string? columns = null)
    {
        var individuals = await _db.Individuals
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var cols = ParseColumns(columns);

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csvBytes = _spreadsheet.ExportIndividualsToCsv(individuals, cols);
            return File(csvBytes, "text/csv", $"individuals_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        var excelBytes = _spreadsheet.ExportIndividualsToExcel(individuals, cols);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"individuals_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpGet("devices")]
    public async Task<IActionResult> ExportDevices([FromQuery] string format = "xlsx", [FromQuery] string? columns = null)
    {
        var devices = await _db.Devices
            .OrderBy(d => d.DeviceName)
            .ToListAsync();

        var cols = ParseColumns(columns);

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csvBytes = _spreadsheet.ExportDevicesToCsv(devices, cols);
            return File(csvBytes, "text/csv", $"devices_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        var excelBytes = _spreadsheet.ExportDevicesToExcel(devices, cols);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"devices_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> ExportSessions([FromQuery] string format = "xlsx", [FromQuery] string? columns = null)
    {
        var sessions = await _db.Sessions
            .Include(s => s.Individual)
            .Include(s => s.Device)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        var cols = ParseColumns(columns);

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csvBytes = _spreadsheet.ExportSessionsToCsv(sessions, cols);
            return File(csvBytes, "text/csv", $"sessions_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        var excelBytes = _spreadsheet.ExportSessionsToExcel(sessions, cols);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"sessions_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpPost("individuals")]
    public async Task<IActionResult> ImportIndividuals(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".csv"))
        {
            return BadRequest(new { message = "Unsupported file type. Please upload an .xlsx or .csv file." });
        }

        using var stream = file.OpenReadStream();
        var result = _spreadsheet.ImportIndividuals(stream, file.FileName);

        if (result.Errors.Count > 0 && result.Rows.Count == 0)
        {
            return BadRequest(new { message = "Import failed with validation errors.", errors = result.Errors });
        }

        var adminId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
        var imported = 0;
        var skipped = 0;

        foreach (var row in result.Rows)
        {
            if (await _db.Individuals.AnyAsync(i => i.EmailOrEmployeeId == row.EmailOrEmployeeId))
            {
                skipped++;
                continue;
            }

            var status = row.Status?.Equals("Registered", StringComparison.OrdinalIgnoreCase) == true
                ? IndividualStatus.Registered
                : IndividualStatus.Pending;

            _db.Individuals.Add(new Individual
            {
                FullName = row.FullName,
                EmailOrEmployeeId = row.EmailOrEmployeeId,
                Department = row.Department,
                Sex = row.Sex,
                Age = row.Age,
                Province = row.Province,
                CityMunicipality = row.CityMunicipality,
                Barangay = row.Barangay,
                Sectors = row.Sectors,
                ServiceAvailed = row.ServiceAvailed,
                Status = status,
                CreatedByAdminId = adminId,
                CreatedAt = DateTime.UtcNow
            });

            imported++;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            imported,
            skipped,
            totalRows = result.Rows.Count,
            errors = result.Errors
        });
    }

    [HttpPost("devices")]
    public async Task<IActionResult> ImportDevices(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".csv"))
        {
            return BadRequest(new { message = "Unsupported file type. Please upload an .xlsx or .csv file." });
        }

        using var stream = file.OpenReadStream();
        var result = _spreadsheet.ImportDevices(stream, file.FileName);

        if (result.Errors.Count > 0 && result.Rows.Count == 0)
        {
            return BadRequest(new { message = "Import failed with validation errors.", errors = result.Errors });
        }

        var imported = 0;
        var skipped = 0;

        foreach (var row in result.Rows)
        {
            if (await _db.Devices.AnyAsync(d => d.DeviceFingerprint == row.DeviceFingerprint))
            {
                skipped++;
                continue;
            }

            var status = row.Status?.Equals("Unlocked", StringComparison.OrdinalIgnoreCase) == true
                ? DeviceStatus.Unlocked
                : DeviceStatus.Locked;

            _db.Devices.Add(new Device
            {
                DeviceName = row.DeviceName,
                DeviceFingerprint = row.DeviceFingerprint,
                Location = row.Location,
                Status = status,
                LastSeenAt = DateTime.UtcNow
            });

            imported++;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            imported,
            skipped,
            totalRows = result.Rows.Count,
            errors = result.Errors
        });
    }
}
