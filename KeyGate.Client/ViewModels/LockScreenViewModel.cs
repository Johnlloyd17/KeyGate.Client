using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KeyGate.Client.Models;
using KeyGate.Client.Services;

namespace KeyGate.Client.ViewModels;

public class LockScreenViewModel : INotifyPropertyChanged
{
    private readonly ApiService _api;
    private readonly LocalCacheService _cache;
    private readonly SessionMonitorService _monitor;
    private readonly AppSettings _settings;
    private readonly IWindowManager _windowManager;
    private IDispatcherTimer? _configTimer;
    private IDispatcherTimer? _logoutCheckTimer;
    private CancellationTokenSource? _trayMinimizeCts;
    private string? _scheduledLogoutTime;
    private bool _hasTriggeredLogoutToday;
    private int _lastKnownConfigVersion;

    private string _keyEntry = string.Empty;
    private string _title = "KeyGate";
    private string _subtitle = "Enter your access key to unlock this computer";
    private string? _statusMessage;
    private string? _userName;
    private bool _isBusy;
    private bool _isUnlocked;
    private bool _isSuccessVisible;
    private int _activeSessionId;
    private ImageSource? _backgroundImage;
    private ImageSource? _logoImage;
    private bool _initialized;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LockScreenViewModel(
        ApiService api,
        DeviceIdentityService identity,
        LocalCacheService cache,
        SessionMonitorService monitor,
        AppSettings settings)
    {
        _api = api;
        _cache = cache;
        _monitor = monitor;
        _settings = settings;
        _windowManager = IWindowManager.Current ?? new StubWindowManager();

        _monitor.IdleDetected += OnIdleDetected;

        UnlockCommand = new Command(async () => await UnlockAsync());
        LockCommand = new Command(async () => await LockAsync());
        NotifyActivityCommand = new Command(() => NotifyActivity());

#if WINDOWS
        if (_windowManager is Platforms.Windows.WindowsWindowManager winManager)
        {
            winManager.OnTrayRestoreRequested += async () => await TrayRestoreAsync();
            winManager.OnTrayLockAndExitRequested += async () => await TrayLockAndExitAsync();
        }
#endif
    }

    public ICommand UnlockCommand { get; }
    public ICommand LockCommand { get; }
    public ICommand NotifyActivityCommand { get; }

    public string KeyEntry
    {
        get => _keyEntry;
        set
        {
            if (SetProperty(ref _keyEntry, value))
            {
                NotifyActivity();
            }
        }
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string? UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool IsUnlocked
    {
        get => _isUnlocked;
        set
        {
            if (SetProperty(ref _isUnlocked, value))
            {
                OnPropertyChanged(nameof(IsLocked));
            }
        }
    }

    public bool IsLocked => !IsUnlocked;

    public bool IsSuccessVisible
    {
        get => _isSuccessVisible;
        set => SetProperty(ref _isSuccessVisible, value);
    }

    public int ActiveSessionId
    {
        get => _activeSessionId;
        set => SetProperty(ref _activeSessionId, value);
    }

    public ImageSource? BackgroundImage
    {
        get => _backgroundImage;
        set => SetProperty(ref _backgroundImage, value);
    }

    public ImageSource? LogoImage
    {
        get => _logoImage;
        set => SetProperty(ref _logoImage, value);
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        var cached = await _cache.GetLockScreenConfigAsync();
        if (cached is not null)
        {
            ApplyConfig(cached);
        }

        try
        {
            await _api.EnsureDeviceRegisteredAsync();
            _lastKnownConfigVersion = await _api.GetConfigVersionAsync();
            await RefreshConfigAsync();
        }
        catch
        {
            StatusMessage = "Cannot reach the KeyGate server. Showing last known lock screen.";
        }

        _configTimer ??= CreateConfigTimer();
        _configTimer.Start();
    }

    private IDispatcherTimer CreateConfigTimer()
    {
        var timer = Application.Current?.Dispatcher.CreateTimer()
            ?? throw new InvalidOperationException("No dispatcher available.");
        timer.Interval = TimeSpan.FromSeconds(30);
        timer.Tick += async (_, _) => await CheckConfigVersionAsync();
        return timer;
    }

    private async Task CheckConfigVersionAsync()
    {
        try
        {
            var version = await _api.GetConfigVersionAsync();
            if (version > 0 && version != _lastKnownConfigVersion)
            {
                _lastKnownConfigVersion = version;
                await RefreshConfigAsync();
            }
        }
        catch
        {
        }
    }

    private async Task RefreshConfigAsync()
    {
        try
        {
            var config = await _api.GetLockScreenConfigAsync();
            if (config is null)
            {
                return;
            }

            ApplyConfig(config);
            await _cache.SaveLockScreenConfigAsync(config);
        }
        catch
        {
        }
    }

    private void ApplyConfig(LockScreenConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.Title))
        {
            Title = config.Title;
        }

        if (!string.IsNullOrWhiteSpace(config.Subtitle))
        {
            Subtitle = config.Subtitle;
        }

        BackgroundImage = MakeImageSource(config.BackgroundImageUrl);
        LogoImage = MakeImageSource(config.LogoUrl);

        _scheduledLogoutTime = config.ScheduledLogoutTime;
        UpdateLogoutCheckTimer();
    }

    private void UpdateLogoutCheckTimer()
    {
        if (string.IsNullOrWhiteSpace(_scheduledLogoutTime) || !TimeOnly.TryParse(_scheduledLogoutTime, out _))
        {
            _logoutCheckTimer?.Stop();
            _logoutCheckTimer = null;
            return;
        }

        if (_logoutCheckTimer is not null)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        _logoutCheckTimer = dispatcher.CreateTimer();
        _logoutCheckTimer.Interval = TimeSpan.FromSeconds(30);
        _logoutCheckTimer.Tick += OnLogoutCheckTimerTick;
        _logoutCheckTimer.Start();
    }

    private async void OnLogoutCheckTimerTick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_scheduledLogoutTime))
            return;

        if (!TimeOnly.TryParse(_scheduledLogoutTime, out var scheduledTime))
            return;

        var now = TimeOnly.FromDateTime(DateTime.Now);

        if (now.Hour == scheduledTime.Hour && now.Minute == scheduledTime.Minute)
        {
            if (_hasTriggeredLogoutToday)
                return;

            _hasTriggeredLogoutToday = true;

            if (_windowManager.IsInTray)
            {
                _windowManager.RestoreFromTray();
            }

            await LockAsync("ScheduledLogout");
        }
        else
        {
            _hasTriggeredLogoutToday = false;
        }
    }

    private static ImageSource? MakeImageSource(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return new UriImageSource
            {
                Uri = uri,
                CachingEnabled = true,
                CacheValidity = TimeSpan.FromDays(7)
            };
        }

        return null;
    }

    private async Task UnlockAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var key = KeyEntry?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            StatusMessage = "Please enter your access key.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Validating key...";

        try
        {
            var (session, error) = await _api.UnlockAsync(key);
            if (session is not null)
            {
                ActiveSessionId = session.SessionId;
                UserName = session.IndividualName ?? "User";
                KeyEntry = string.Empty;
                StatusMessage = null;
                IsUnlocked = true;
                IsSuccessVisible = true;
                _monitor.Start();

                _ = MinimizeToTrayAfterDelayAsync();
            }
            else
            {
                StatusMessage = error ?? "Invalid access key.";
            }
        }
        catch
        {
            StatusMessage = "Cannot reach the KeyGate server. Check the network connection.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MinimizeToTrayAfterDelayAsync()
    {
        try
        {
            _trayMinimizeCts?.Cancel();
            _trayMinimizeCts = new CancellationTokenSource();
            var token = _trayMinimizeCts.Token;

            await Task.Delay(TimeSpan.FromSeconds(3), token);

            if (!token.IsCancellationRequested)
            {
                _windowManager.MinimizeToTray();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private async Task LockAsync(string endReason = "Logout")
    {
        _monitor.Stop();
        CancelPendingTrayMinimize();

        if (ActiveSessionId > 0)
        {
            await _api.EndSessionAsync(ActiveSessionId, endReason);
        }

        ActiveSessionId = 0;
        UserName = null;
        IsUnlocked = false;
        IsSuccessVisible = false;
        KeyEntry = string.Empty;
        StatusMessage = null;
    }

    private async Task TrayRestoreAsync()
    {
        _windowManager.RestoreFromTray();
        await LockAsync("Logout");
    }

    private async Task TrayLockAndExitAsync()
    {
        _windowManager.RestoreFromTray();
        await LockAsync("Logout");

        Application.Current?.Quit();
    }

    private void CancelPendingTrayMinimize()
    {
        _trayMinimizeCts?.Cancel();
        _trayMinimizeCts = null;
    }

    private void NotifyActivity()
    {
        _monitor.NotifyActivity();
    }

    private async void OnIdleDetected(object? sender, EventArgs e)
    {
        if (_windowManager.IsInTray)
        {
            _windowManager.RestoreFromTray();
        }

        await LockAsync("IdleTimeout");
    }

    private bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
        {
            return false;
        }

        backingField = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
