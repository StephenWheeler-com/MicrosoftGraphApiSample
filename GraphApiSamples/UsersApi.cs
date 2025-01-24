using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Newtonsoft.Json;
using STW.Public.Samples.Microsoft.Azure.KeyVaultNS.Interfaces;
using STW.Public.Samples.Models;

namespace STW.Public.Samples.Microsoft.Azure.Entra.GraphApi.Samples;

public class UsersApi
{
    private readonly string[] scopes = new[] { @"https://graph.microsoft.com/.default" };

    private readonly string clientId = string.Empty;

    private readonly ILogger<UsersApi> logger;

    private readonly IKeyVault keyVault = null!;

    public UsersApi(IKeyVault keyVault, ILogger<UsersApi> logger)
    {
        this.keyVault = keyVault;

        this.logger = logger;

        clientId = Environment.GetEnvironmentVariable("ClientId");
    }

    /// <summary>
    /// Function to return the Entra Id (AAD
    /// Users that match a name prefix.
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    [Function("Users")]
    public async Task<IActionResult> GetUsersAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        EntraUsers? entraUsers = null;

        logger.LogInformation("GetUsersAsync: processing a request.");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var request = JsonConvert.DeserializeObject<UsersRequest>(requestBody);

            if (request?.TenantUId == Guid.Empty)
            {
                return new BadRequestObjectResult("Invalid Tenant Id");
            }
            var tenantUId = request?.TenantUId.ToString("D");

            var filter = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.SearchString))
            {
                filter = @$"startsWith(displayName, '{request.SearchString.Trim().ToLower()}')";
            }

            int maxRowCount = 60;
            if (request.MaxRowCount > 0)
            {
                maxRowCount = request.MaxRowCount;
            }

            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var secretValue = await keyVault.GetClientSecret();

            var clientSecretCredential = new ClientSecretCredential(
            request?.TenantUId.ToString("D"), clientId, secretValue, options);

            var graphServiceClient = new GraphServiceClient(clientSecretCredential, scopes);

            var userList = new List<User>();

            var usersResponse = await graphServiceClient
            .Users
            .GetAsync(requestConfiguration => {
                requestConfiguration.QueryParameters.Filter = filter;
                requestConfiguration.QueryParameters.Count = true;
                requestConfiguration.QueryParameters.Top = maxRowCount;
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
            });

            var usrIterator = PageIterator<User, UserCollectionResponse>
                .CreatePageIterator(graphServiceClient, usersResponse, (user) => { userList.Add(user); return true; });

            await usrIterator.IterateAsync();

            entraUsers = new EntraUsers();

            foreach (var user in userList)
            {
                var entraUser = new EntraUser
                {
                    Id = new Guid(user.Id),
                    DisplayName = user.DisplayName,
                    GivenName = user.GivenName,
                    Surname = user.Surname,
                    UPN = user.UserPrincipalName,
                    PreferredName = user.PreferredName,
                    PreferredLanguage = user.PreferredLanguage,
                    CreatedDateTime = user.CreatedDateTime,
                    Email = user.Mail
                };

                entraUsers.Items.Add(entraUser);
            }
        }
        catch (ODataError ex)
        {
            logger.LogError(ex.ToString());
        }
        catch (ServiceException ex)
        {
            logger.LogError(ex.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());
        }

        return new OkObjectResult(entraUsers);
    }

    /// <summary>
    /// Function to return an Entra Id (AAD) User
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    [Function("User")]
    public async Task<IActionResult> GetUserAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        EntraUser? entraUser = null;

        logger.LogInformation("GetUserAsync: processing a request.");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var request = JsonConvert.DeserializeObject<UserRequest>(requestBody);

            if (request?.TenantUId == Guid.Empty)
            {
                return new BadRequestObjectResult("Invalid Tenant Id");
            }
            var tenantUId = request?.TenantUId.ToString("D");

            if (string.IsNullOrWhiteSpace(request.SearchString))
            {
                return new BadRequestObjectResult("Invalid SearchString Parameter: The SearchString parameter cannot be empty.");
            }

            if (request.SearchString.Length > 256)
            {
                return new BadRequestObjectResult("Invalid SearchString Parameter: The SearchString parameter must be 256 Characters or less.");
            }

            var filter = @$"displayName eq '{request.SearchString.Trim().ToLower()}'";

            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var secretValue = await keyVault.GetClientSecret();

            var clientSecretCredential = new ClientSecretCredential(
            request?.TenantUId.ToString("D"), clientId, secretValue, options);

            // this could be put into a factory method
            var graphServiceClient = new GraphServiceClient(clientSecretCredential, scopes);

            var userResponse = await graphServiceClient
            .Users
            .GetAsync(requestConfiguration => {
                //requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "givenName", "surname", "userPrincipalName", "preferredName", "preferredLanguage", "Mail", "createdDateTime" };
                requestConfiguration.QueryParameters.Filter = filter;
                requestConfiguration.QueryParameters.Count = true;
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
            });

            if (userResponse?.Value?.Count == 1)
            {
                entraUser = new EntraUser
                {
                    Id = new Guid(userResponse.Value[0].Id),
                    DisplayName = userResponse.Value[0].DisplayName,
                    GivenName = userResponse.Value[0].GivenName,
                    Surname = userResponse.Value[0].Surname,
                    UPN = userResponse.Value[0].UserPrincipalName,
                    PreferredName = userResponse.Value[0].PreferredName,
                    PreferredLanguage = userResponse.Value[0].PreferredLanguage,
                    CreatedDateTime = userResponse.Value[0].CreatedDateTime,
                    Email = userResponse.Value[0].Mail
                };

                return new OkObjectResult(entraUser);
            }

            return new NotFoundObjectResult($"No matching user was found.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());

            return new ObjectResult(new { statusCode = 500, Message = ex.Message });
        }
    }
}
