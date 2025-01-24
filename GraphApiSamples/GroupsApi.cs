using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Newtonsoft.Json;
using STW.Public.GraphApiSamples.Common.Enums;
using STW.Public.Samples.Microsoft.Azure.KeyVaultNS.Interfaces;
using STW.Public.Samples.Models;
using System.Text;

namespace STW.Public.Samples.Microsoft.Azure.Entra.GraphApi.Samples;

public class GroupsApi
{
    private readonly string[] scopes = new[] { @"https://graph.microsoft.com/.default" };

    private readonly string clientId = string.Empty;

    private readonly ILogger<GroupsApi> logger;

    private readonly IKeyVault keyVault = null!;

    public GroupsApi(IKeyVault keyVault, ILogger<GroupsApi> logger)
    {
        this.keyVault = keyVault;

        this.logger = logger;

        clientId = Environment.GetEnvironmentVariable("ClientId");
    }

    /// <summary>
    /// Function to get Entra (AAD) Groups
    /// matching a Group Name prefix
    /// </summary>
    /// <param name="req">GroupsRequest</param>
    /// <returns></returns>
    [Function("Groups")]
    public async Task<IActionResult> GetGroupsAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        EntraGroups? entraGroups = null;

        logger.LogInformation("GetGroupsAsync: processing a request.");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var request = JsonConvert.DeserializeObject<GroupsRequest>(requestBody);

            if(request?.TenantUId == Guid.Empty)
            {
                return new BadRequestObjectResult("Invalid Tenant Id");
            }
            var tenantUId = request?.TenantUId.ToString("D");

            var filter = new StringBuilder();
            var groupType = request?.GroupType;
            switch(groupType)
            {
                case EntraGroupTypes.SecurityGroup:
                    filter.AppendFormat(@$"mailEnabled eq false and securityEnabled eq true");
                    break;

                default:
                    filter.AppendFormat(@$"groupTypes/any(c: c eq 'Unified')");
                    break;
            }

            var searchString = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.SearchString))
            {
                searchString = request.SearchString.Trim().ToLower();
                filter.AppendFormat(@$" and startsWith(displayName, '{searchString}')");
            }

            int maxRowCount = 60;
            if(request.MaxRowCount > 0)
            {
                maxRowCount = request.MaxRowCount;
            }

            var secretValue = await keyVault.GetClientSecret();

            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var clientSecretCredential = new ClientSecretCredential(
            request?.TenantUId.ToString("D"), clientId, secretValue, options);

            var graphServiceClient = new GraphServiceClient(clientSecretCredential, scopes);

            var groupList = new List<Group>();

            var groupsResponse = await graphServiceClient
            .Groups
            .GetAsync(requestConfiguration => {
                requestConfiguration.QueryParameters.Filter = filter.ToString();
                requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "description", "Mail", "Members", "createdDateTime" };
                requestConfiguration.QueryParameters.Count = true;
                requestConfiguration.QueryParameters.Top = maxRowCount;
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
            });

            var grpIterator = PageIterator<Group, GroupCollectionResponse>
                .CreatePageIterator(graphServiceClient, groupsResponse, (group) => { groupList.Add(group); return true; });

            await grpIterator.IterateAsync();

            entraGroups = new EntraGroups();

            foreach (var group in groupList)
            {
                var entraGroup = new EntraGroup
                {
                    Id = new Guid(group.Id),
                    DisplayName = group.DisplayName,
                    Description = group.Description,
                    Email = group.Mail
                };

                entraGroups.Items.Add(entraGroup);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());
        }

        return new OkObjectResult(entraGroups);
    }

    /// <summary>
    /// Function to get Entra (AAD) Group
    /// matching a Provided Group Name
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    [Function("Group")]
    public async Task<IActionResult> GetGroupAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        EntraGroup? entraGroup = null;

        logger.LogInformation("GetGroupAsync: processing a request.");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var request = JsonConvert.DeserializeObject<GroupRequest>(requestBody);

            if (request?.TenantUId == Guid.Empty)
            {
                return new BadRequestObjectResult("Invalid Tenant Id");
            }
            var tenantUId = request?.TenantUId.ToString("D");

            if (string.IsNullOrWhiteSpace(request?.SearchString))
            {
                return new BadRequestObjectResult("Invalid SearchString Parameter: The SearchString parameter cannot be empty.");
            }

            if (request.SearchString.Length > 256)
            {
                return new BadRequestObjectResult("Invalid SearchString Parameter: The SearchString parameter must be 256 Characters or less.");
            }

            var filter = new StringBuilder();
            var groupType = request?.GroupType;
            switch (groupType)
            {
                case EntraGroupTypes.SecurityGroup:
                    filter.AppendFormat(@$"mailEnabled eq false and securityEnabled eq true and ");
                    break;

                case EntraGroupTypes.O365Group:
                    filter.AppendFormat(@$"groupTypes/any(c: c eq 'Unified') and ");
                    break;
            }

            var searchString = string.Empty;

            searchString = request?.SearchString.Trim().ToLower();
            filter.AppendFormat(@$"displayName eq '{searchString}'");

            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var secretValue = await keyVault.GetClientSecret();

            var clientSecretCredential = new ClientSecretCredential(
            request?.TenantUId.ToString("D"), clientId, secretValue, options);

            // This could go into a factory method
            var graphServiceClient = new GraphServiceClient(clientSecretCredential, scopes);

            var groupList = new List<Group>();

            var groupResponse = await graphServiceClient
                .Groups
                .GetAsync(requestConfiguration => {
                    requestConfiguration.QueryParameters.Filter = filter.ToString();
                    requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "description", "Mail", "Members", "createdDateTime" };
                    requestConfiguration.QueryParameters.Count = true;
                    requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                });

            if (groupResponse?.Value?.Count == 1)
            {
                entraGroup = new EntraGroup
                {
                    Id = new Guid(groupResponse.Value[0].Id),
                    DisplayName = groupResponse.Value[0].DisplayName,
                    Description = groupResponse.Value[0].Description,
                    Email = groupResponse.Value[0].Mail
                };

                return new OkObjectResult(entraGroup);
            }

            return new NotFoundObjectResult($"No matching group was found.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());

            return new ObjectResult(new { statusCode = 500, Message = ex.Message });
        }
    }

    /// <summary>
    /// Function to Return the members of an EntraId (AAD) Group
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    [Function("GroupMembers")]
    public async Task<IActionResult> GetGroupMembersAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        EntraGroup? entraGroup = null;

        logger.LogInformation("GetGroupMembersAsync: processing a request.");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var request = JsonConvert.DeserializeObject<GroupRequest>(requestBody);

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

            var filter = new StringBuilder();
            var groupType = request?.GroupType;
            switch (groupType)
            {
                case EntraGroupTypes.SecurityGroup:
                    filter.AppendFormat(@$"mailEnabled eq false and securityEnabled eq true and ");
                    break;

                case EntraGroupTypes.O365Group:
                    filter.AppendFormat(@$"groupTypes/any(c: c eq 'Unified') and ");
                    break;
            }

            var searchString = string.Empty;

            searchString = request.SearchString.Trim().ToLower();
            filter.AppendFormat(@$"displayName eq '{searchString}'");

            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var secretValue = await keyVault.GetClientSecret();
            var clientSecretCredential = new ClientSecretCredential(
            request?.TenantUId.ToString("D"), clientId, secretValue, options);

            // This could go into a factory method
            var graphServiceClient = new GraphServiceClient(clientSecretCredential, scopes);

            var groupResponse = await graphServiceClient
                .Groups
                .GetAsync(requestConfiguration => {
                    requestConfiguration.QueryParameters.Filter = filter.ToString();
                    requestConfiguration.QueryParameters.Select = new string[] { "id" };
                    requestConfiguration.QueryParameters.Count = true;
                    requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                });

            if (groupResponse?.Value?.Count == 1)
            {
                var membersResponse = await graphServiceClient
                    .Groups[groupResponse.Value[0].Id]
                    .Members
                    .GetAsync(requestConfiguration => {
                        requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "mail", "memberOf", "JobTitle", "AccountEnabled", "userPrincipalName", "createdDateTime" };
                        requestConfiguration.QueryParameters.Top = 100;
                        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                    });

                var entraUsers = new EntraUsers();

                if (membersResponse?.Value?.Count > 0)
                {
                    var userList = new List<DirectoryObject>();

                    var usrIterator = PageIterator<DirectoryObject, DirectoryObjectCollectionResponse>
                        .CreatePageIterator(graphServiceClient, membersResponse, (user) => { userList.Add(user); return true; });

                    await usrIterator.IterateAsync();

                    foreach (User user in userList)
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

                return new OkObjectResult(entraUsers);
            }

            return new NotFoundObjectResult($"No matching group was found.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());

            return new ObjectResult(new { statusCode = 500, Message = ex.Message });
        }
    }
}
