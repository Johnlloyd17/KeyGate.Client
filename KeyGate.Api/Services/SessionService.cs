using KeyGate.Api.Data;
using KeyGate.Api.Entities;
using KeyGate.Api.Hubs;
using KeyGate.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeyGate.Api.Services;

public enum UnlockFailureReason
{
    None,
    DeviceNotFound,
    DeviceUnlocked,
    InvalidKey,
    NotRegistered,
    KeyActiveElsewhere,
    ConcurrentConflict
}

public record UnlockResult(
    bool Succeeded,
    UnlockFailureReason FailureReason,
    string? Message,
    int? SessionId,
    string? IndividualName);

public class SessionService
{
    private readonly KeyGateDbContext _db;
    private readonly KeyHashingService _hashing;
    private readonly IHubContext<DeviceStatusHub> _hub;

    public SessionService(KeyGateDbContext db, KeyHashingService hashing, IHubContext<DeviceStatusHub> hub)
    {
        _db = db;
        _hashing = hashing;
        _hub = hub;
    }

    public async Task<UnlockResult> UnlockAsync(int deviceId, string key)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        try
        {
            var device = await _db.Devices.SingleOrDefaultAsync(d => d.Id == deviceId);
            if (device is null)
            {
                return new UnlockResult(false, UnlockFailureReason.DeviceNotFound, "Device not found.", null, null);
            }

            if (device.Status == DeviceStatus.Unlocked)
            {
                return new UnlockResult(false, UnlockFailureReason.DeviceUnlocked, "This device is already unlocked.", null, null);
            }

            var activeKeys = await _db.AccessKeys
                .Include(k => k.Individual)
                .Where(k => k.IsActive)
                .ToListAsync();

            var matchingKey = activeKeys.FirstOrDefault(k => _hashing.Verify(key, k.KeyHash));
            if (matchingKey is null)
            {
                return new UnlockResult(false, UnlockFailureReason.InvalidKey, "Invalid access key.", null, null);
            }

            if (matchingKey.Individual.Status != IndividualStatus.Registered)
            {
                return new UnlockResult(false, UnlockFailureReason.NotRegistered, "This individual is not registered.", null, null);
            }

            var hasActiveSession = await _db.Sessions.AnyAsync(s =>
                s.IndividualId == matchingKey.IndividualId && s.EndedAt == null);

            if (hasActiveSession)
            {
                return new UnlockResult(false, UnlockFailureReason.KeyActiveElsewhere, "This key is already active on another device.", null, null);
            }

            var session = new Session
            {
                IndividualId = matchingKey.IndividualId,
                DeviceId = device.Id,
                StartedAt = DateTime.UtcNow
            };

            _db.Sessions.Add(session);
            device.Status = DeviceStatus.Unlocked;
            device.LastSeenAt = DateTime.UtcNow;
            matchingKey.LastUsedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            await BroadcastStatusChangedAsync(device.Id, device.DeviceName, DeviceStatus.Unlocked, matchingKey.Individual.FullName);

            return new UnlockResult(true, UnlockFailureReason.None, null, session.Id, matchingKey.Individual.FullName);
        }
        catch (PostgresException ex) when (ex.SqlState == "40001")
        {
            await transaction.RollbackAsync();
            return new UnlockResult(false, UnlockFailureReason.ConcurrentConflict, "Too many unlock attempts at once, please try again.", null, null);
        }
    }

    public async Task<Session?> EndSessionAsync(int sessionId)
    {
        var session = await _db.Sessions
            .Include(s => s.Device)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null || session.EndedAt is not null)
        {
            return session;
        }

        session.EndedAt = DateTime.UtcNow;
        session.DurationSeconds = (int)(session.EndedAt.Value - session.StartedAt).TotalSeconds;
        if (session.Device is not null)
        {
            session.Device.Status = DeviceStatus.Locked;
            session.Device.LastSeenAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        if (session.Device is not null)
        {
            await BroadcastStatusChangedAsync(session.Device.Id, session.Device.DeviceName, DeviceStatus.Locked, null);
        }

        return session;
    }

    private async Task BroadcastStatusChangedAsync(int deviceId, string deviceName, DeviceStatus status, string? currentIndividualName)
    {
        var @event = new DeviceStatusHub.DeviceStatusChangedEvent(
            deviceId,
            deviceName,
            status.ToString(),
            currentIndividualName,
            DateTime.UtcNow);

        await _hub.Clients.All.SendAsync(DeviceStatusHub.DeviceStatusChangedMethod, @event);
    }
}
