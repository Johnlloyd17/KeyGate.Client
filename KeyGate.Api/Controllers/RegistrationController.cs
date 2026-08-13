using System.Security.Cryptography;
using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyGate.Api.Controllers;

[ApiController]
[Route("api/registration")]
public class RegistrationController : ControllerBase
{
    private readonly KeyGateDbContext _db;
    private readonly KeyHashingService _hashing;

    public RegistrationController(KeyGateDbContext db, KeyHashingService hashing)
    {
        _db = db;
        _hashing = hashing;
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

        return Ok(new CompleteRegistrationResponse(
            accessKey,
            registrationToken.Individual.FullName,
            registrationToken.Individual.EmailOrEmployeeId,
            DateTime.UtcNow));
    }

    private static string GenerateAccessKey() =>
        RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
}
