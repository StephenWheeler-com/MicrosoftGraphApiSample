using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using STW.Public.Samples.Microsoft.Azure.KeyVaultNS.Interfaces;

namespace STW.Public.Samples.Microsoft.Azure.KeyVaultNS;

public class KeyVault : IKeyVault
{
    private readonly ILogger<KeyVault> _logger;

    private readonly string keyVaultName = string.Empty;

    private readonly string secretName = string.Empty;

    public KeyVault(ILogger<KeyVault> logger)
    {
        _logger = logger;

        keyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");

        secretName = Environment.GetEnvironmentVariable("SecretName");
    }

    public async Task<string> GetClientSecret()
    {
        #if DEBUG
            // This is used when local debugging
            return Environment.GetEnvironmentVariable("ClientSecret");
        #endif

        var vaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");

        var client = new SecretClient(vaultUri: vaultUri, credential: new DefaultAzureCredential());

        var clientSecret = await client.GetSecretAsync(secretName);

        return clientSecret.Value.Value;
    }
}
