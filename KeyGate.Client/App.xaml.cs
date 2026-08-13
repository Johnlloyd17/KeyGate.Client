using KeyGate.Client.Views;

namespace KeyGate.Client
{
    public partial class App : Application
    {
        private readonly LockScreenPage _lockScreenPage;

        public App(LockScreenPage lockScreenPage)
        {
            InitializeComponent();
            _lockScreenPage = lockScreenPage;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(_lockScreenPage)
            {
                Title = "KeyGate"
            };

#if WINDOWS
            window.Created += OnWindowCreated;
#endif

            return window;
        }

#if WINDOWS
        private static void OnWindowCreated(object? sender, EventArgs e)
        {
            KeyGate.Client.WinUI.StartupRegistration.EnsureEnabled();

            if (sender is not Window window || window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
            {
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
        }
#endif
    }
}
