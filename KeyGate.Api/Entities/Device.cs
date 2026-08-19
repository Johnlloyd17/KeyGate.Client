namespace KeyGate.Api.Entities;

public enum DeviceStatus
{
    Locked = 0,
    Unlocked = 1
}

public class Device
{
    public int Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceApiKeyHash { get; set; }
    public string? Location { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Locked;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public List<LockScreenConfig> LockScreenConfigs { get; set; } = new();
    public List<ConfigChangeLog> ConfigChangeLogs { get; set; } = new();
    public List<Session> Sessions { get; set; } = new();
}
