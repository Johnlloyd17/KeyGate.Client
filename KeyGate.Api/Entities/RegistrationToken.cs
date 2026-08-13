namespace KeyGate.Api.Entities;

public class RegistrationToken
{
    public int Id { get; set; }
    public int IndividualId { get; set; }
    public Individual Individual { get; set; } = null!;
    public Guid Token { get; set; } = Guid.NewGuid();
    public string QrCodeUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
