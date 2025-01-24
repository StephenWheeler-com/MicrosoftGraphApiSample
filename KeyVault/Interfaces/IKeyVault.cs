namespace STW.Public.Samples.Microsoft.Azure.KeyVaultNS.Interfaces;

public interface IKeyVault
{
    Task<string> GetClientSecret();
}
