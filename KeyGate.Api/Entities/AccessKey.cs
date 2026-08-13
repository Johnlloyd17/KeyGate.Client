namespace KeyGate.Api.Entities;

public class AccessKey
{
    public int Id { get; set; }
    public int IndividualId { get; set; }
    public Individual Individual { get; set; } = null!;
    public string KeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
