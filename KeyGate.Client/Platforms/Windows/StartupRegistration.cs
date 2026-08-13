using Microsoft.Win32;

namespace KeyGate.Client.WinUI
{
    public static class StartupRegistration
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "KeyGate.Client";

        public static void EnsureEnabled()
        {
            try
            {
                var processPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    return;
                }

                using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (runKey is null)
                {
                    return;
                }

                var command = "\"" + processPath + "\"";
                if (!string.Equals(runKey.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase))
                {
                    runKey.SetValue(ValueName, command);
                }
            }
            catch
            {
            }
        }
    }
}
