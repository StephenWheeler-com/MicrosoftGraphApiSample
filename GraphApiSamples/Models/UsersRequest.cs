namespace STW.Public.Samples.Models;

public class UsersRequest
{
    public Guid TenantUId { get; set; }

    public int MaxRowCount { get; set; }

    public string? SearchString { get; set; } = string.Empty;
}
