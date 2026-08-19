using System.Security.Cryptography;
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
[Route("api/registration")]
public class RegistrationController : ControllerBase
{
    private readonly KeyGateDbContext _db;
    private readonly KeyHashingService _hashing;
    private readonly QrCodeService _qrCodeService;
    private readonly IHubContext<DeviceStatusHub> _hub;

    public RegistrationController(KeyGateDbContext db, KeyHashingService hashing, QrCodeService qrCodeService, IHubContext<DeviceStatusHub> hub)
    {
        _db = db;
        _hashing = hashing;
        _qrCodeService = qrCodeService;
        _hub = hub;
    }

    public record RegistrationInfoDto(
        Guid Token,
        string FullName,
        string EmailOrEmployeeId,
        string? Department,
        DateTime ExpiresAt,
        bool IsUsed);

    public record CompleteRegistrationResponse(
        string AccessKey,
        string FullName,
        string EmailOrEmployeeId,
        DateTime CompletedAt);

    public record SelfRegisterRequest(
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

    [HttpGet("{token:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRegistrationInfo(Guid token)
    {
        var registrationToken = await _db.RegistrationTokens
            .Include(t => t.Individual)
            .SingleOrDefaultAsync(t => t.Token == token);

        if (registrationToken is null)
        {
            return NotFound(new { message = "Registration token not found." });
        }

        if (registrationToken.IsUsed)
        {
            return BadRequest(new { message = "This registration token has already been used." });
        }

        if (registrationToken.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new { message = "This registration token has expired." });
        }

        return Ok(new RegistrationInfoDto(
            registrationToken.Token,
            registrationToken.Individual.FullName,
            registrationToken.Individual.EmailOrEmployeeId,
            registrationToken.Individual.Department,
            registrationToken.ExpiresAt,
            registrationToken.IsUsed));
    }

    [HttpPost("{token:guid}/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteRegistration(Guid token)
    {
        var registrationToken = await _db.RegistrationTokens
            .Include(t => t.Individual)
            .SingleOrDefaultAsync(t => t.Token == token);

        if (registrationToken is null)
        {
            return NotFound(new { message = "Registration token not found." });
        }

        if (registrationToken.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new { message = "This registration token has expired." });
        }

        if (await _db.AccessKeys.AnyAsync(k => k.IndividualId == registrationToken.IndividualId && k.IsActive))
        {
            return BadRequest(new { message = "This individual already has an active access key." });
        }

        var claimed = await _db.RegistrationTokens
            .Where(t => t.Id == registrationToken.Id && !t.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsUsed, true));

        if (claimed == 0)
        {
            return BadRequest(new { message = "This registration token has already been used." });
        }

        var accessKey = GenerateAccessKey();

        var accessKeyEntity = new AccessKey
        {
            IndividualId = registrationToken.IndividualId,
            KeyHash = _hashing.Hash(accessKey),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.AccessKeys.Add(accessKeyEntity);
        registrationToken.Individual.Status = IndividualStatus.Registered;

        await _db.SaveChangesAsync();

        await BroadcastIndividualChangedAsync("Updated", registrationToken.Individual.Id, registrationToken.Individual.FullName, registrationToken.Individual.Status);

        return Ok(new CompleteRegistrationResponse(
            accessKey,
            registrationToken.Individual.FullName,
            registrationToken.Individual.EmailOrEmployeeId,
            DateTime.UtcNow));
    }

    [HttpPost("self-register")]
    [AllowAnonymous]
    public async Task<IActionResult> SelfRegister([FromBody] SelfRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.EmailOrEmployeeId))
        {
            return BadRequest(new { message = "Full name and Email / Employee ID are required." });
        }

        if (await _db.Individuals.AnyAsync(i => i.EmailOrEmployeeId == request.EmailOrEmployeeId))
        {
            return BadRequest(new { message = "An individual with that email/ID already exists." });
        }

        var individual = new Individual
        {
            FullName = request.FullName.Trim(),
            EmailOrEmployeeId = request.EmailOrEmployeeId.Trim(),
            Department = request.Department?.Trim(),
            Sex = request.Sex?.Trim(),
            Age = request.Age,
            Province = request.Province?.Trim(),
            CityMunicipality = request.CityMunicipality?.Trim(),
            Barangay = request.Barangay?.Trim(),
            Sectors = request.Sectors,
            ServiceAvailed = request.ServiceAvailed?.Trim(),
            Status = IndividualStatus.Registered,
            CreatedByAdminId = null,
            CreatedAt = DateTime.UtcNow
        };

        _db.Individuals.Add(individual);
        await _db.SaveChangesAsync();

        var accessKey = GenerateAccessKey();

        var accessKeyEntity = new AccessKey
        {
            IndividualId = individual.Id,
            KeyHash = _hashing.Hash(accessKey),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.AccessKeys.Add(accessKeyEntity);
        await _db.SaveChangesAsync();

        await BroadcastIndividualChangedAsync("Created", individual.Id, individual.FullName, individual.Status);

        return Ok(new CompleteRegistrationResponse(
            accessKey,
            individual.FullName,
            individual.EmailOrEmployeeId,
            DateTime.UtcNow));
    }

    private static string GenerateAccessKey() =>
        RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");

    [HttpPost("qr")]
    [AllowAnonymous]
    public IActionResult GenerateQr([FromBody] QrRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { message = "URL is required." });
        }

        var png = _qrCodeService.GenerateQrCodePng(request.Url);
        return Ok(new { qrCodePngBase64 = Convert.ToBase64String(png) });
    }

    public record QrRequest(string Url);

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
