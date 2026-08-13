namespace KeyGate.Api.Services;

public class KeyHashingService
{
    public string Hash(string value) => BCrypt.Net.BCrypt.HashPassword(value);

    public bool Verify(string value, string hash) => BCrypt.Net.BCrypt.Verify(value, hash);
}
