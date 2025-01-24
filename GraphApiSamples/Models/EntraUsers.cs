namespace STW.Public.Samples.Models;

public class EntraUsers
{
    public EntraUsers()
    {
        Items = new List<EntraUser>();
    }

    public List<EntraUser> Items { get; set; } = null!;
}
