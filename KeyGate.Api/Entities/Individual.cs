namespace KeyGate.Api.Entities;

public enum IndividualStatus
{
    Pending = 0,
    Registered = 1
}

public class Individual
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string EmailOrEmployeeId { get; set; } = string.Empty;
    public string? Department { get; set; }
    public IndividualStatus Status { get; set; } = IndividualStatus.Pending;
    public int CreatedByAdminId { get; set; }
    public Admin CreatedByAdmin { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RegistrationToken> RegistrationTokens { get; set; } = new();
    public List<AccessKey> AccessKeys { get; set; } = new();
    public List<Session> Sessions { get; set; } = new();
}
