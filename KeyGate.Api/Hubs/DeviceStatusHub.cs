using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KeyGate.Api.Hubs;

[Authorize(Roles = "Admin")]
public class DeviceStatusHub : Hub
{
    public record DeviceStatusChangedEvent(
        int DeviceId,
        string DeviceName,
        string Status,
        string? CurrentIndividualName,
        DateTime ChangedAt);

    public const string DeviceStatusChangedMethod = "DeviceStatusChanged";
}
