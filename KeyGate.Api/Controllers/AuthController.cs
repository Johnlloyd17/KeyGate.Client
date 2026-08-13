using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KeyGate.Api.Data;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace KeyGate.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly KeyGateDbContext _db;
    private readonly KeyHashingService _hashing;
    private readonly IConfiguration _configuration;

    public AuthController(KeyGateDbContext db, KeyHashingService hashing, IConfiguration configuration)
    {
        _db = db;
        _hashing = hashing;
        _configuration = configuration;
    }

    public record LoginRequest(string Email, string Password);

    public record LoginResponse(string Token, DateTime ExpiresAt, string FullName, string Email, string Role);

    [HttpPost("admin/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var admin = await _db.Admins.SingleOrDefaultAsync(a => a.Email == request.Email);
        if (admin is null || !_hashing.Verify(request.Password, admin.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var jwtSection = _configuration.GetSection("Jwt");
        var expiry = DateTime.UtcNow.AddMinutes(
            int.Parse(jwtSection["ExpiryMinutes"] ?? "480"));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, admin.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, admin.FullName),
            new Claim(ClaimTypes.Role, admin.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: credentials);

        var serialized = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new LoginResponse(
            serialized,
            expiry,
            admin.FullName,
            admin.Email,
            admin.Role));
    }
}
