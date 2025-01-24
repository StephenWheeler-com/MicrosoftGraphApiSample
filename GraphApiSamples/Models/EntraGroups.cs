namespace STW.Public.Samples.Models;

public class EntraGroups
{
    public EntraGroups()
    {
        Items = new List<EntraGroup>();
    }

    public List<EntraGroup> Items { get; set; } = null!;
}
