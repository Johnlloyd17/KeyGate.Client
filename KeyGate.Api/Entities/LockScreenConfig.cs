namespace KeyGate.Api.Entities;

public class LockScreenConfig
{
    public int Id { get; set; }
    public int? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? Title { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
