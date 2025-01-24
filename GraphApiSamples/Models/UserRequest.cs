namespace STW.Public.Samples.Models;

public class UserRequest
{
    public Guid TenantUId { get; set; }

    public string? SearchString { get; set; } = string.Empty;
}
