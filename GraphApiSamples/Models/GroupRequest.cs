using STW.Public.GraphApiSamples.Common.Enums;

namespace STW.Public.Samples.Models;

public class GroupRequest
{
    public Guid TenantUId { get; set; }

    public EntraGroupTypes GroupType { get; set; }

    public string? SearchString { get; set; } = string.Empty;
}
