namespace KeyGate.Client.Services;

public interface IUserActivityMonitor
{
    DateTime GetLastInputTimeUtc();
}

public class StubUserActivityMonitor : IUserActivityMonitor
{
    public DateTime GetLastInputTimeUtc() => DateTime.UtcNow;
}
