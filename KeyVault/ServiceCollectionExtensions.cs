using Microsoft.Extensions.DependencyInjection;
using STW.Public.Samples.Microsoft.Azure.KeyVaultNS.Interfaces;

namespace STW.Public.Samples.Microsoft.Azure.KeyVaultNS;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseKeyVault(this IServiceCollection services)
    {
        services.AddScoped<IKeyVault, KeyVault>();

        return services;
    }
}
