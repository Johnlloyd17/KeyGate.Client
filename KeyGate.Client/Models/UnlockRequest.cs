namespace KeyGate.Client.Models;

public class UnlockRequest
{
    public string Key { get; set; } = string.Empty;
    public int DeviceId { get; set; }
}
