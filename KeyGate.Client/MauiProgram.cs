using KeyGate.Client.Services;
using KeyGate.Client.ViewModels;
using KeyGate.Client.Views;
using Microsoft.Extensions.Logging;

namespace KeyGate.Client
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<AppSettings>();
            builder.Services.AddSingleton<DeviceIdentityService>();
            builder.Services.AddSingleton<LocalCacheService>();
            builder.Services.AddSingleton<SessionMonitorService>();
            builder.Services.AddSingleton<ApiService>();

#if WINDOWS
            builder.Services.AddSingleton<SystemTrayService>();
            builder.Services.AddSingleton<IWindowManager, Platforms.Windows.WindowsWindowManager>();
            builder.Services.AddSingleton<IUserActivityMonitor, Platforms.Windows.WindowsUserActivityMonitor>();
#else
            builder.Services.AddSingleton<IWindowManager, StubWindowManager>();
            builder.Services.AddSingleton<IUserActivityMonitor, StubUserActivityMonitor>();
#endif

            builder.Services.AddSingleton<LockScreenViewModel>();
            builder.Services.AddSingleton<LockScreenPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
