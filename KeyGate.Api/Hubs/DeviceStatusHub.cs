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

    public record IndividualChangedEvent(
        string Action,
        int Id,
        string FullName,
        string Status,
        DateTime ChangedAt);

    public record SessionChangedEvent(
        string Action,
        int SessionId,
        int IndividualId,
        string IndividualName,
        int DeviceId,
        string DeviceName,
        DateTime StartedAt,
        DateTime? EndedAt,
        DateTime ChangedAt);

    public record LockScreenConfigChangedEvent(
        int? DeviceId,
        DateTime ChangedAt);

    public record DeviceChangedEvent(
        string Action,
        int DeviceId,
        string DeviceName,
        string Status,
        DateTime ChangedAt);

    public const string DeviceStatusChangedMethod = "DeviceStatusChanged";
    public const string IndividualChangedMethod = "IndividualChanged";
    public const string SessionChangedMethod = "SessionChanged";
    public const string LockScreenConfigChangedMethod = "LockScreenConfigChanged";
    public const string DeviceChangedMethod = "DeviceChanged";
}
