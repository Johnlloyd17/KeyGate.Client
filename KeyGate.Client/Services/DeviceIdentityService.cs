namespace KeyGate.Client.Services;

public class DeviceIdentityService
{
    private const string FingerprintKey = "kg_device_fingerprint";
    private const string DeviceIdKey = "kg_device_id";
    private const string ApiKeyKey = "kg_device_api_key";

    public string GetDeviceFingerprint()
    {
        var fingerprint = Preferences.Default.Get(FingerprintKey, string.Empty);
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            fingerprint = "KG-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            Preferences.Default.Set(FingerprintKey, fingerprint);
        }
        return fingerprint;
    }

    public string GetDeviceName() => $"{AppSettings.Current.DeviceNamePrefix}-{Environment.MachineName}";

    public bool IsRegistered => GetDeviceId() is not null && !string.IsNullOrWhiteSpace(GetDeviceApiKey());

    public int? GetDeviceId()
    {
        var id = Preferences.Default.Get(DeviceIdKey, 0);
        return id > 0 ? id : null;
    }

    public string? GetDeviceApiKey() => Preferences.Default.Get(ApiKeyKey, string.Empty);

    public void SetDeviceCredentials(int deviceId, string apiKey)
    {
        Preferences.Default.Set(DeviceIdKey, deviceId);
        Preferences.Default.Set(ApiKeyKey, apiKey);
    }

    public void ClearDeviceCredentials()
    {
        Preferences.Default.Remove(DeviceIdKey);
        Preferences.Default.Remove(ApiKeyKey);
    }
}
