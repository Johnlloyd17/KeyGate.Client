using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
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
[Route("api/individuals")]
[Authorize(Roles = "Admin")]
public class IndividualsController : ControllerBase
{
    private readonly KeyGateDbContext _db;
    private readonly QrCodeService _qrCodeService;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<DeviceStatusHub> _hub;

    public IndividualsController(KeyGateDbContext db, QrCodeService qrCodeService, IConfiguration configuration, IHubContext<DeviceStatusHub> hub)
    {
        _db = db;
        _qrCodeService = qrCodeService;
        _configuration = configuration;
        _hub = hub;
    }

    public record CreateIndividualRequest(string FullName, string EmailOrEmployeeId, string? Department);

    public record UpdateIndividualRequest(
        string FullName,
        string EmailOrEmployeeId,
        string? Department,
        string? Sex,
        int? Age,
        string? Province,
        string? CityMunicipality,
        string? Barangay,
        string? Sectors,
        string? ServiceAvailed);

    public record RegistrationTokenDto(Guid Token, string QrCodeUrl, DateTime ExpiresAt, bool IsUsed, string? QrCodePngBase64);

    public record IndividualDto(
        int Id,
        string FullName,
        string EmailOrEmployeeId,
        string? Department,
        string? Sex,
        int? Age,
        string? Province,
        string? CityMunicipality,
        string? Barangay,
        string? Sectors,
        string? ServiceAvailed,
        string Status,
        DateTime CreatedAt,
        RegistrationTokenDto? RegistrationToken);

    [HttpGet]
    public async Task<IActionResult> GetIndividuals()
    {
        var individuals = await _db.Individuals
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var individualIds = individuals.Select(i => i.Id).ToList();

        var latestTokens = await _db.RegistrationTokens
            .Where(t => individualIds.Contains(t.IndividualId))
            .GroupBy(t => t.IndividualId)
            .Select(g => g.OrderByDescending(t => t.CreatedAt).First())
            .ToListAsync();

        var tokenLookup = latestTokens.ToDictionary(t => t.IndividualId, t => t);

        var result = individuals.Select(i => new IndividualDto(
            i.Id,
            i.FullName,
            i.EmailOrEmployeeId,
            i.Department,
            i.Sex,
            i.Age,
            i.Province,
            i.CityMunicipality,
            i.Barangay,
            i.Sectors,
            i.ServiceAvailed,
            i.Status.ToString(),
            i.CreatedAt,
            tokenLookup.TryGetValue(i.Id, out var token) ? ToTokenDto(token, includeQrCode: false) : null))
            .ToList();

        return Ok(result);
    }

    [HttpGet("dropdown")]
    public async Task<IActionResult> GetIndividualsDropdown()
    {
        var items = await _db.Individuals
            .OrderBy(i => i.FullName)
            .Select(i => new { i.Id, i.FullName })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetIndividual(int id)
    {
        var individual = await _db.Individuals
            .Include(i => i.RegistrationTokens)
            .SingleOrDefaultAsync(i => i.Id == id);

        if (individual is null)
        {
            return NotFound(new { message = "Individual not found." });
        }

        return Ok(new IndividualDto(
            individual.Id,
            individual.FullName,
            individual.EmailOrEmployeeId,
            individual.Department,
            individual.Sex,
            individual.Age,
            individual.Province,
            individual.CityMunicipality,
            individual.Barangay,
            individual.Sectors,
            individual.ServiceAvailed,
            individual.Status.ToString(),
            individual.CreatedAt,
            individual.RegistrationTokens
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => ToTokenDto(t, includeQrCode: false))
                .FirstOrDefault()));
    }

    [HttpPost]
    public async Task<IActionResult> CreateIndividual([FromBody] CreateIndividualRequest request)
    {
        if (await _db.Individuals.AnyAsync(i => i.EmailOrEmployeeId == request.EmailOrEmployeeId))
        {
            return Conflict(new { message = "An individual with that email/ID already exists." });
        }

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var individual = new Individual
        {
            FullName = request.FullName,
            EmailOrEmployeeId = request.EmailOrEmployeeId,
            Department = request.Department,
            Status = IndividualStatus.Pending,
            CreatedByAdminId = adminId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Individuals.Add(individual);
        await _db.SaveChangesAsync();

        var token = await CreateRegistrationTokenAsync(individual.Id);

        var result = new IndividualDto(
            individual.Id,
            individual.FullName,
            individual.EmailOrEmployeeId,
            individual.Department,
            individual.Sex,
            individual.Age,
            individual.Province,
            individual.CityMunicipality,
            individual.Barangay,
            individual.Sectors,
            individual.ServiceAvailed,
            individual.Status.ToString(),
            individual.CreatedAt,
            ToTokenDto(token, includeQrCode: true));

        await BroadcastIndividualChangedAsync("Created", individual.Id, individual.FullName, individual.Status);

        return CreatedAtAction(nameof(GetIndividual), new { id = individual.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateIndividual(int id, [FromBody] UpdateIndividualRequest request)
    {
        var individual = await _db.Individuals.FindAsync(id);
        if (individual is null)
        {
            return NotFound(new { message = "Individual not found." });
        }

        if (await _db.Individuals.AnyAsync(i => i.EmailOrEmployeeId == request.EmailOrEmployeeId && i.Id != id))
        {
            return Conflict(new { message = "An individual with that email/ID already exists." });
        }

        individual.FullName = request.FullName;
        individual.EmailOrEmployeeId = request.EmailOrEmployeeId;
        individual.Department = request.Department;
        individual.Sex = request.Sex;
        individual.Age = request.Age;
        individual.Province = request.Province;
        individual.CityMunicipality = request.CityMunicipality;
        individual.Barangay = request.Barangay;
        individual.Sectors = request.Sectors;
        individual.ServiceAvailed = request.ServiceAvailed;

        await _db.SaveChangesAsync();

        await BroadcastIndividualChangedAsync("Updated", individual.Id, individual.FullName, individual.Status);

        var latestToken = await _db.RegistrationTokens
            .Where(t => t.IndividualId == id)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        return Ok(new IndividualDto(
            individual.Id,
            individual.FullName,
            individual.EmailOrEmployeeId,
            individual.Department,
            individual.Sex,
            individual.Age,
            individual.Province,
            individual.CityMunicipality,
            individual.Barangay,
            individual.Sectors,
            individual.ServiceAvailed,
            individual.Status.ToString(),
            individual.CreatedAt,
            latestToken is null ? null : ToTokenDto(latestToken, includeQrCode: false)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteIndividual(int id)
    {
        var individual = await _db.Individuals.FindAsync(id);
        if (individual is null)
        {
            return NotFound(new { message = "Individual not found." });
        }

        if (await _db.Sessions.AnyAsync(s => s.IndividualId == id))
        {
            return Conflict(new { message = "Cannot delete: this individual has session history (session logs are immutable)." });
        }

        _db.Individuals.Remove(individual);
        await _db.SaveChangesAsync();

        await BroadcastIndividualChangedAsync("Deleted", individual.Id, individual.FullName, individual.Status);

        return NoContent();
    }

    [HttpPost("{id:int}/regenerate-token")]
    public async Task<IActionResult> RegenerateToken(int id)
    {
        var individual = await _db.Individuals.FindAsync(id);
        if (individual is null)
        {
            return NotFound(new { message = "Individual not found." });
        }

        var unusedTokens = await _db.RegistrationTokens
            .Where(t => t.IndividualId == id && !t.IsUsed)
            .ToListAsync();

        foreach (var unusedToken in unusedTokens)
        {
            unusedToken.IsUsed = true;
        }
        await _db.SaveChangesAsync();

        var token = await CreateRegistrationTokenAsync(id);

        return Ok(new
        {
            id = individual.Id,
            fullName = individual.FullName,
            status = individual.Status.ToString(),
            registrationToken = ToTokenDto(token, includeQrCode: true)
        });
    }

    private async Task<RegistrationToken> CreateRegistrationTokenAsync(int individualId)
    {
        var token = new RegistrationToken
        {
            IndividualId = individualId,
            Token = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddHours(
                int.Parse(_configuration.GetSection("AppSettings")["RegistrationTokenLifetimeHours"] ?? "48")),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        var baseUrl = _configuration.GetSection("AppSettings")["RegistrationBaseUrl"] ?? "http://localhost:5000";
        var registrationUrl = $"{baseUrl.TrimEnd('/')}/register/{token.Token}";
        token.QrCodeUrl = registrationUrl;

        _db.RegistrationTokens.Add(token);
        await _db.SaveChangesAsync();

        return token;
    }

    private RegistrationTokenDto ToTokenDto(RegistrationToken token, bool includeQrCode)
    {
        return new RegistrationTokenDto(
            token.Token,
            token.QrCodeUrl,
            token.ExpiresAt,
            token.IsUsed,
            includeQrCode ? Convert.ToBase64String(_qrCodeService.GenerateQrCodePng(token.QrCodeUrl)) : null);
    }

    private async Task BroadcastIndividualChangedAsync(string action, int id, string fullName, IndividualStatus status)
    {
        var @event = new DeviceStatusHub.IndividualChangedEvent(
            action,
            id,
            fullName,
            status.ToString(),
            DateTime.UtcNow);

        await _hub.Clients.All.SendAsync(DeviceStatusHub.IndividualChangedMethod, @event);
    }
}
