using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyGate.Api.Controllers;

[ApiController]
[Route("api/admin/account")]
[Authorize(Roles = "Admin")]
public class AdminAccountController : ControllerBase
{
    private readonly KeyGateDbContext _db;
    private readonly KeyHashingService _hashing;
    private readonly IWebHostEnvironment _environment;

    public AdminAccountController(KeyGateDbContext db, KeyHashingService hashing, IWebHostEnvironment environment)
    {
        _db = db;
        _hashing = hashing;
        _environment = environment;
    }

    public record AccountDto(int Id, string FullName, string Email, string Role, string? Phone, string? AvatarUrl, string? Position, DateTime CreatedAt);

    public record UpdateProfileRequest(string FullName, string Email, string? Phone, string? Position);

    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var adminId = GetAdminId();
        var admin = await _db.Admins.FindAsync(adminId);
        if (admin is null) return NotFound(new { message = "Admin not found." });

        return Ok(new AccountDto(admin.Id, admin.FullName, admin.Email, admin.Role, admin.Phone, admin.AvatarUrl, admin.Position, admin.CreatedAt));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var adminId = GetAdminId();
        var admin = await _db.Admins.FindAsync(adminId);
        if (admin is null) return NotFound(new { message = "Admin not found." });

        if (await _db.Admins.AnyAsync(a => a.Email == request.Email && a.Id != adminId))
        {
            return Conflict(new { message = "An admin with that email already exists." });
        }

        admin.FullName = request.FullName;
        admin.Email = request.Email;
        admin.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone;
        admin.Position = string.IsNullOrWhiteSpace(request.Position) ? null : request.Position;

        await _db.SaveChangesAsync();

        return Ok(new AccountDto(admin.Id, admin.FullName, admin.Email, admin.Role, admin.Phone, admin.AvatarUrl, admin.Position, admin.CreatedAt));
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Unsupported file type. Allowed: PNG, JPG, JPEG, GIF, WEBP." });
        }

        var uploadsDir = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "avatars");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"avatar_{GetAdminId()}_{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"{Request.Scheme}://{Request.Host}/uploads/avatars/{fileName}";

        var adminId = GetAdminId();
        var admin = await _db.Admins.FindAsync(adminId);
        if (admin is not null)
        {
            admin.AvatarUrl = url;
            await _db.SaveChangesAsync();
        }

        return Ok(new { url });
    }

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var adminId = GetAdminId();
        var admin = await _db.Admins.FindAsync(adminId);
        if (admin is null) return NotFound(new { message = "Admin not found." });

        if (!_hashing.Verify(request.CurrentPassword, admin.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "New password must be at least 6 characters." });
        }

        admin.PasswordHash = _hashing.Hash(request.NewPassword);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password changed successfully." });
    }

    private int GetAdminId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new UnauthorizedAccessException());
    }
}
