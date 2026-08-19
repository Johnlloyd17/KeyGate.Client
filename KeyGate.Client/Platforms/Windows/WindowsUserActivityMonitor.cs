using System.Runtime.InteropServices;
using KeyGate.Client.Services;

namespace KeyGate.Client.Platforms.Windows;

public class WindowsUserActivityMonitor : IUserActivityMonitor
{
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public DateTime GetLastInputTimeUtc()
    {
        try
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (GetLastInputInfo(ref info) && info.dwTime > 0)
            {
                long elapsedMs = (long)Environment.TickCount - info.dwTime;
                if (elapsedMs < 0)
                {
                    elapsedMs = 0;
                }

                return DateTime.UtcNow - TimeSpan.FromMilliseconds(elapsedMs);
            }
        }
        catch
        {
        }

        return DateTime.UtcNow;
    }
}
