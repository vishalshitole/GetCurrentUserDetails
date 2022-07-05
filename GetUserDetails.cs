using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Graph;
using Azure.Identity;

namespace MMC.GetUserDetails
{
    public static class GetUserDetails
    {
        [FunctionName("GetUserDetails")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string UserPrincipalName = req.Query["upn"];

            if(string.IsNullOrEmpty(UserPrincipalName))
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                UserPrincipalName = UserPrincipalName ?? data?.upn;
            }

            // GET https://graph.microsoft.com/v1.0/me?$select=displayName,jobTitle
            var tenantId = Environment.GetEnvironmentVariable("Azure_App_Tenant_Id");
            var clientId = Environment.GetEnvironmentVariable("Azure_App_Client_Id");
            var clientSecret = Environment.GetEnvironmentVariable("Azure_App_Client_Secret");
            var graphClient = GetGraphClient(tenantId, clientId, clientSecret);
            var user = await graphClient.Users[UserPrincipalName]
                .Request()
                .Select(u => new {
                    u.DisplayName,
                    u.JobTitle,
                    u.EmployeeId, 
                    u.UserPrincipalName
                })
                .GetAsync();

            return new OkObjectResult(user);
        }

        public static GraphServiceClient GetGraphClient(string tenantId, string clientId, string clientSecret) 
        {
            // The client credentials flow requires that you request the
            // /.default scope, and preconfigure your permissions on the
            // app registration in Azure. An administrator must grant consent
            // to those permissions beforehand.
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);

            var graphClient = new GraphServiceClient(clientSecretCredential, scopes);
            return graphClient;
        }
    }
}
