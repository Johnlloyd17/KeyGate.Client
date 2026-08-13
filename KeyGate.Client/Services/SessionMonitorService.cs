namespace KeyGate.Client.Services;

public class SessionMonitorService
{
    private readonly AppSettings _settings;
    private IDispatcherTimer? _timer;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private bool _isMonitoring;

    public event EventHandler? IdleDetected;

    public SessionMonitorService(AppSettings settings)
    {
        _settings = settings;
    }

    public bool IsMonitoring => _isMonitoring;

    public void Start()
    {
        Stop();

        _isMonitoring = true;
        _lastActivityUtc = DateTime.UtcNow;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(15);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    public void NotifyActivity()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    public void Stop()
    {
        _isMonitoring = false;
        if (_timer is not null)
        {
            _timer.Tick -= OnTimerTick;
            _timer.Stop();
            _timer = null;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_isMonitoring)
        {
            return;
        }

        if ((DateTime.UtcNow - _lastActivityUtc).TotalMinutes >= _settings.IdleTimeoutMinutes)
        {
            Stop();
            IdleDetected?.Invoke(this, EventArgs.Empty);
        }
    }
}
