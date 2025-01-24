using STW.Public.GraphApiSamples.Common.Enums;

namespace STW.Public.Samples.Models;

public class GroupMembersRequest
{
    public Guid TenantUId { get; set; }

    public EntraGroupTypes GroupType { get; set; }

    public int MaxRowCount { get; set; }

    public string? SearchString { get; set; } = string.Empty;
}
