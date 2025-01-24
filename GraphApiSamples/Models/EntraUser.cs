namespace STW.Public.Samples.Models;

public class EntraUser
{
    public Guid Id { get; set; } = Guid.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string GivenName { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public string UPN { get; set; } = string.Empty;

    public string PreferredName { get; set; } = string.Empty;

    public string PreferredLanguage { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset? CreatedDateTime { get; set; } = DateTimeOffset.MinValue;
    // Status?
}
