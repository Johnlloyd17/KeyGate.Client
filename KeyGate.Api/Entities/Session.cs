namespace KeyGate.Api.Entities;

public enum SessionEndReason
{
    Logout = 0,
    IdleTimeout = 1,
    ForcedByAdmin = 2
}

public class Session
{
    public int Id { get; set; }
    public int IndividualId { get; set; }
    public Individual Individual { get; set; } = null!;
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public SessionEndReason? EndReason { get; set; }
}
