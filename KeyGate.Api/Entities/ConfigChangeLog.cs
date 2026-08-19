namespace KeyGate.Api.Entities;

public class ConfigChangeLog
{
    public int Id { get; set; }
    public int? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string? ChangedBy { get; set; }
    public string FieldChanged { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
