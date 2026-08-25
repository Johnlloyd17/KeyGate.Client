using System.Security.Claims;
using KeyGate.Admin.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace KeyGate.Admin.Services;

public class DeviceStatusListener
{
    private readonly IConfiguration _configuration;
    private readonly AuthenticationStateProvider _authStateProvider;

    private HubConnection? _connection;
    private bool _started;

    public event Action<DeviceStatusChangedEvent>? DeviceStatusChanged;
    public event Action<IndividualChangedEvent>? IndividualChanged;
    public event Action<SessionChangedEvent>? SessionChanged;
    public event Action<LockScreenConfigChangedEvent>? LockScreenConfigChanged;
    public event Action<DeviceChangedEvent>? DeviceChanged;

    public DeviceStatusListener(IConfiguration configuration, AuthenticationStateProvider authStateProvider)
    {
        _configuration = configuration;
        _authStateProvider = authStateProvider;
    }

    public async Task EnsureStartedAsync()
    {
        if (_started)
        {
            return;
        }

        var baseUrl = _configuration["KeyGateApi:BaseUrl"] ?? "http://localhost:5000";
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var jwt = authState.User.FindFirstValue(AdminApiClient.JwtClaimType);

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl.TrimEnd('/')}/hubs/devices", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(jwt);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<DeviceStatusChangedEvent>("DeviceStatusChanged", OnDeviceStatusChanged);
        _connection.On<IndividualChangedEvent>("IndividualChanged", OnIndividualChanged);
        _connection.On<SessionChangedEvent>("SessionChanged", OnSessionChanged);
        _connection.On<LockScreenConfigChangedEvent>("LockScreenConfigChanged", OnLockScreenConfigChanged);
        _connection.On<DeviceChangedEvent>("DeviceChanged", OnDeviceChanged);

        await _connection.StartAsync();
        _started = true;
    }

    private void OnDeviceStatusChanged(DeviceStatusChangedEvent @event)
    {
        try { DeviceStatusChanged?.Invoke(@event); } catch { }
    }

    private void OnIndividualChanged(IndividualChangedEvent @event)
    {
        try { IndividualChanged?.Invoke(@event); } catch { }
    }

    private void OnSessionChanged(SessionChangedEvent @event)
    {
        try { SessionChanged?.Invoke(@event); } catch { }
    }

    private void OnLockScreenConfigChanged(LockScreenConfigChangedEvent @event)
    {
        try { LockScreenConfigChanged?.Invoke(@event); } catch { }
    }

    private void OnDeviceChanged(DeviceChangedEvent @event)
    {
        try { DeviceChanged?.Invoke(@event); } catch { }
    }
}
