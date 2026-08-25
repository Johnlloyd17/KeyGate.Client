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
    string? Sex,
    int? Age,
    string? Province,
    string? CityMunicipality,
    string? Barangay,
    string? Sectors,
    string? ServiceAvailed,
    string Status,
    DateTime CreatedAt,
    RegistrationTokenDto? RegistrationToken);

public record CreateIndividualRequest(string FullName, string EmailOrEmployeeId, string? Department);

public record UpdateIndividualRequest(
    string FullName,
    string EmailOrEmployeeId,
    string? Department,
    string? Sex,
    int? Age,
    string? Province,
    string? CityMunicipality,
    string? Barangay,
    string? Sectors,
    string? ServiceAvailed);

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
    string? Subtitle,
    string? ScheduledLogoutTime,
    DateTime UpdatedAt,
    string Source);

public record SaveLockScreenConfigRequest(
    int? DeviceId,
    string? BackgroundImageUrl,
    string? LogoUrl,
    string? Title,
    string? Subtitle,
    string? ScheduledLogoutTime);

public record ConfigChangeLogDto(
    int Id,
    int? DeviceId,
    string? ChangedBy,
    string FieldChanged,
    string? OldValue,
    string? NewValue,
    DateTime ChangedAt);

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

public record ImportResult(
    int Imported,
    int Skipped,
    int TotalRows,
    List<string> Errors);

public record AdminAccountDto(
    int Id,
    string FullName,
    string Email,
    string Role,
    string? Phone,
    string? AvatarUrl,
    string? Position,
    DateTime CreatedAt);

public record UpdateAdminProfileRequest(
    string FullName,
    string Email,
    string? Phone,
    string? Position);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record DropdownItem(int Id, string Name);
