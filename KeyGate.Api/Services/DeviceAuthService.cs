using System.Security.Cryptography;
using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KeyGate.Api.Services;

public class DeviceAuthService
{
    private readonly KeyGateDbContext _db;
    private readonly KeyHashingService _hashing;

    public DeviceAuthService(KeyGateDbContext db, KeyHashingService hashing)
    {
        _db = db;
        _hashing = hashing;
    }

    public static string IssueApiKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public string HashApiKey(string apiKey) => _hashing.Hash(apiKey);

    public async Task<Device?> ValidateAsync(int? deviceId, string? apiKey)
    {
        if (deviceId is null || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var device = await _db.Devices.SingleOrDefaultAsync(d => d.Id == deviceId);
        if (device is null || string.IsNullOrEmpty(device.DeviceApiKeyHash))
        {
            return null;
        }

        if (!_hashing.Verify(apiKey, device.DeviceApiKeyHash))
        {
            return null;
        }

        device.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return device;
    }
}
