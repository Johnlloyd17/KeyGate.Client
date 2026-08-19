namespace KeyGate.Client.Services;

public interface IWindowManager
{
    static IWindowManager? Current { get; set; }

    void MinimizeToTray();
    void RestoreFromTray();
    void EnterFullScreen();
    bool IsInTray { get; }
}

public class StubWindowManager : IWindowManager
{
    public bool IsInTray => false;
    public void MinimizeToTray() { }
    public void RestoreFromTray() { }
    public void EnterFullScreen() { }
}
