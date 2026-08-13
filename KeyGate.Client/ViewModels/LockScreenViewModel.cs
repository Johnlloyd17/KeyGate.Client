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
    private readonly IDispatcherTimer _configTimer;

    private string _keyEntry = string.Empty;
    private string _title = "KeyGate";
    private string? _statusMessage;
    private string? _userName;
    private bool _isBusy;
    private bool _isUnlocked;
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

        _monitor.IdleDetected += OnIdleDetected;

        _configTimer = Application.Current?.Dispatcher.CreateTimer() ?? throw new InvalidOperationException("No dispatcher available.");
        _configTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, _settings.ConfigRefreshMinutes));
        _configTimer.Tick += async (_, _) => await RefreshConfigAsync();

        UnlockCommand = new Command(async () => await UnlockAsync());
        LockCommand = new Command(async () => await LockAsync());
        NotifyActivityCommand = new Command(() => NotifyActivity());
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
            await RefreshConfigAsync();
        }
        catch
        {
            StatusMessage = "Cannot reach the KeyGate server. Showing last known lock screen.";
        }

        _configTimer.Start();
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

        BackgroundImage = MakeImageSource(config.BackgroundImageUrl);
        LogoImage = MakeImageSource(config.LogoUrl);
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
                _monitor.Start();
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

    private async Task LockAsync(string endReason = "Logout")
    {
        _monitor.Stop();

        if (ActiveSessionId > 0)
        {
            await _api.EndSessionAsync(ActiveSessionId, endReason);
        }

        ActiveSessionId = 0;
        UserName = null;
        IsUnlocked = false;
        KeyEntry = string.Empty;
        StatusMessage = null;
    }

    private void NotifyActivity()
    {
        _monitor.NotifyActivity();
    }

    private async void OnIdleDetected(object? sender, EventArgs e)
    {
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
