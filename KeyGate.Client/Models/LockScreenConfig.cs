namespace KeyGate.Client.Models;

public class LockScreenConfig
{
    public int? DeviceId { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? Title { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Source { get; set; } = "default";
}
