using System.Security.Claims;
using KeyGate.Admin.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace KeyGate.Admin.Services;

public class DeviceStatusListener : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly AuthenticationStateProvider _authStateProvider;

    private HubConnection? _connection;
    private bool _started;

    public event Action<DeviceStatusChangedEvent>? DeviceStatusChanged;

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
            .Build();

        _connection.On<DeviceStatusChangedEvent>("DeviceStatusChanged", OnDeviceStatusChanged);

        await _connection.StartAsync();
        _started = true;
    }

    private void OnDeviceStatusChanged(DeviceStatusChangedEvent @event)
    {
        DeviceStatusChanged?.Invoke(@event);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
            _started = false;
        }
    }
}
