namespace KeyGate.Admin.Models;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, DateTime ExpiresAt, string FullName, string Email, string Role);

public record RegistrationTokenDto(
    Guid Token,
    string QrCodeUrl,
    DateTime ExpiresAt,
    bool IsUsed,
    string? QrCodePngBase64);

public record IndividualDto(
    int Id,
    string FullName,
    string EmailOrEmployeeId,
    string? Department,
    string Status,
    DateTime CreatedAt,
    RegistrationTokenDto? RegistrationToken);

public record CreateIndividualRequest(string FullName, string EmailOrEmployeeId, string? Department);

public record UpdateIndividualRequest(string FullName, string EmailOrEmployeeId, string? Department);

public record RegenerateTokenResponse(int Id, string FullName, string Status, RegistrationTokenDto RegistrationToken);

public record DeviceDto(
    int Id,
    string DeviceName,
    string DeviceFingerprint,
    string? Location,
    string Status,
    DateTime LastSeenAt,
    int? CurrentSessionId,
    string? CurrentIndividualName);

public record UpdateDeviceRequest(string? DeviceName, string? Location);

public record LockScreenConfigDto(
    int? DeviceId,
    string? BackgroundImageUrl,
    string? LogoUrl,
    string? Title,
    DateTime UpdatedAt,
    string Source);

public record SaveLockScreenConfigRequest(int? DeviceId, string? BackgroundImageUrl, string? LogoUrl, string? Title);

public record UploadImageResponse(string Url);

public record SessionDto(
    int Id,
    int IndividualId,
    string IndividualName,
    int DeviceId,
    string DeviceName,
    DateTime StartedAt,
    DateTime? EndedAt,
    int? DurationSeconds,
    string? EndReason);

public record DeviceStatusChangedEvent(
    int DeviceId,
    string DeviceName,
    string Status,
    string? CurrentIndividualName,
    DateTime ChangedAt);
